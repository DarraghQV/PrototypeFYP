using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using System.Collections.Generic;

public class MoveToGoalAgentV1 : Agent
{
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Material winMaterial;
    [SerializeField] private Material loseMaterial;
    [SerializeField] private MeshRenderer agentMeshRenderer;

    private Rigidbody agentRigidbody;
    [SerializeField] private float jumpForce = 5f;
    private bool isGrounded;
    private bool canJumpOnJumpHelp;

    private GameObject wallInstance;
    [SerializeField] private GameObject wallPrefab;

    private float cumulativeReward = 0f;
    private int stepCount = 0;
    private int extraGoalsCollected = 0;
    private bool isExtraGoalTraining = false;
    private bool isNewPlatformActive = false; // Track if the new platform is active

    private List<ExtraGoal> extraGoals = new List<ExtraGoal>(); // To keep track of extra goals
    private List<Vector3> originalPositions = new List<Vector3>(); // Store original positions of extra goals

    private void Start()
    {
        agentRigidbody = GetComponent<Rigidbody>();
        agentRigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    // Called at the beginning of each episode
    public override void OnEpisodeBegin()
    {
        cumulativeReward = 0f;
        stepCount = 0;
        extraGoalsCollected = 0;

        // Initialize the position variables
        float currentWallHeight = Academy.Instance.EnvironmentParameters.GetWithDefault("wall_height", 0.0f);
        isExtraGoalTraining = currentWallHeight == -1.0f;
        isNewPlatformActive = currentWallHeight > 5.25f;

        // **Adjust Jump Force**
        jumpForce = isExtraGoalTraining ? 0f : 10f;
        Debug.Log($"Jump Force Set To: {jumpForce}");

        Vector3 agentPosition;
        if (isExtraGoalTraining)
        {
            // **Spawn agent at (-41, 0, 0) for Extra Goals Training**
            agentPosition = new Vector3(-41f, 0f, 0f);
            Debug.Log("Agent in Extra Goals Training, spawned at (-41, 0, 0)");
        }
        else if (isNewPlatformActive)
        {
            // **Spawn on the new platform at (50, 5, 0)**
            agentPosition = new Vector3(50f, 5f, 0f);
            Debug.Log("Agent spawned on the new platform.");
        }
        else
        {
            // **Normal spawn at (-3.5, 0, 0)**
            agentPosition = new Vector3(-3.5f, 0f, 0f);
            Debug.Log("Agent spawned at the normal position.");
        }

        transform.position = agentPosition;

        // **Adjust target position**
        if (isExtraGoalTraining)
        {
            targetTransform.position = new Vector3(0, -0.25f, 0); // Placeholder target
        }
        else
        {
            targetTransform.position = new Vector3(5f, -0.25f, -4f);
        }

        // **Handle Wall**
        if (currentWallHeight > 0 && !isNewPlatformActive)
        {
            if (wallInstance == null)
            {
                wallInstance = Instantiate(wallPrefab);
            }
            wallInstance.transform.position = new Vector3(0, currentWallHeight / 2f, 0);
            wallInstance.transform.localScale = new Vector3(1f, currentWallHeight, 15f);
        }
        else
        {
            if (wallInstance != null)
            {
                Destroy(wallInstance);
                wallInstance = null;
            }
        }

        Debug.Log($"[INFO] Wall Height: {currentWallHeight}, New Platform: {isNewPlatformActive}, Extra Goal Training: {isExtraGoalTraining}");

        // Store original positions of ExtraGoals and reset them
        extraGoals.Clear(); // Ensure the list is cleared at the start of each episode
        originalPositions.Clear(); // Clear old data before adding new ones

        extraGoals.AddRange(FindObjectsOfType<ExtraGoal>());

        // Store the original positions of the extra goals
        foreach (var extraGoal in extraGoals)
        {
            originalPositions.Add(extraGoal.transform.position);  // Save original positions
        }

        // Make sure all ExtraGoals are visible again (we’ll move them later when collected)
        foreach (var extraGoal in extraGoals)
        {
            extraGoal.gameObject.SetActive(true);  // Ensure extra goals are visible
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(isGrounded);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        stepCount++;
        cumulativeReward += GetCumulativeReward();

        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        int jumpAction = actions.DiscreteActions[0];

        Vector3 movement = new Vector3(moveX, 0, moveZ) * 10f * Time.deltaTime;
        agentRigidbody.MovePosition(transform.position + movement);

        if (jumpAction == 1 && (isGrounded || canJumpOnJumpHelp))
        {
            Jump();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Horizontal");
        continuousActions[1] = Input.GetAxisRaw("Vertical");

        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }

    private void Jump()
    {
        if (isGrounded || canJumpOnJumpHelp)
        {
            agentRigidbody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
            canJumpOnJumpHelp = false;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("ground"))
        {
            isGrounded = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<Goal>(out Goal goal))
        {
            // Reward and End Episode for regular Goal
            if (!isExtraGoalTraining) // Only end episode if NOT in extra goal training
            {
                float explorationBonus = Academy.Instance.EnvironmentParameters.GetWithDefault("curiosity_strength", 0.0f);
                SetReward(3.0f + explorationBonus);
                agentMeshRenderer.material = winMaterial;
                Destroy(goal.gameObject); // Optionally destroy goal
                EndEpisode();
            }
        }

        if (other.TryGetComponent<ExtraGoal>(out ExtraGoal extraGoal))
        {
            // Reward for ExtraGoal and move it to a hidden location
            SetReward(1.5f); // Adjust this value as needed
            agentMeshRenderer.material = winMaterial;

            extraGoalsCollected++;

            // Move the ExtraGoal to a hidden location
            extraGoal.transform.position = new Vector3(100f, 100f, 100f);  // A place far from the agent

            // End Episode ONLY after collecting 3 Extra Goals
            if (isExtraGoalTraining && extraGoalsCollected >= 3)
            {
                EndEpisode();
            }
        }

        if (other.TryGetComponent<Wall>(out Wall wall))
        {
            SetReward(-2f);
            agentMeshRenderer.material = loseMaterial;
            EndEpisode();
        }
    }

    void Update()
    {
        RotateTowardsMouse();

        float meanReward = stepCount > 0 ? cumulativeReward / stepCount : 0f;
        Debug.Log($"[INFO] Steps: {stepCount}, Mean Reward: {meanReward}, Extra Goal Training: {isExtraGoalTraining}");
    }

    private void RotateTowardsMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 targetPoint = ray.GetPoint(distance);
            Vector3 direction = targetPoint - transform.position;
            direction.y = 0;

            if (direction.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
            }
        }
    }

    // Restore positions at the start of the episode
    private void RestoreExtraGoalPositions()
    {
        for (int i = 0; i < extraGoals.Count; i++)
        {
            ExtraGoal goal = extraGoals[i];
            goal.transform.position = originalPositions[i];  // Restore original position
        }
    }

    // Trigger the restoration of ExtraGoal positions manually when needed
    public void ResetExtraGoalPositions()
    {
        RestoreExtraGoalPositions();
    }

    // Override this to manually call ResetExtraGoalPositions after the episode ends
    public void ManuallyTriggerEndEpisode()
    {
        ResetExtraGoalPositions();
        EndEpisode();
    }


private void OnDrawGizmos()
    {
        Gizmos.color = isExtraGoalTraining ? Color.blue : isNewPlatformActive ? Color.green : Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(1f, 1f, 1f));
    }
}
