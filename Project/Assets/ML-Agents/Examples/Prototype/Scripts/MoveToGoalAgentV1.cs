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
    private bool isNewPlatformActive = false;

    private List<ExtraGoal> extraGoals = new List<ExtraGoal>();

    private void Start()
    {
        agentRigidbody = GetComponent<Rigidbody>();
        agentRigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        CacheExtraGoals();
    }

    private void CacheExtraGoals()
    {
        // Find all ExtraGoal objects in the scene, including inactive ones
        ExtraGoal[] allExtraGoals = Resources.FindObjectsOfTypeAll<ExtraGoal>();
        extraGoals.Clear();

        foreach (ExtraGoal goal in allExtraGoals)
        {
            // Only include objects in the scene hierarchy (not prefabs)
            if (goal.gameObject.scene.IsValid())
            {
                extraGoals.Add(goal);
            }
        }
    }

    public override void OnEpisodeBegin()
    {
        cumulativeReward = 0f;
        stepCount = 0;
        extraGoalsCollected = 0;

        float currentWallHeight = Academy.Instance.EnvironmentParameters.GetWithDefault("wall_height", 0.0f);
        isExtraGoalTraining = currentWallHeight == -1.0f;
        isNewPlatformActive = currentWallHeight > 5.25f;

        jumpForce = isExtraGoalTraining ? 0f : 10f;
        Debug.Log($"Jump Force Set To: {jumpForce}");

        Vector3 agentPosition;
        if (isExtraGoalTraining)
        {
            agentPosition = new Vector3(-41f, 0f, 0f);
            Debug.Log("Agent in Extra Goals Training, spawned at (-41, 0, 0)");
        }
        else if (isNewPlatformActive)
        {
            agentPosition = new Vector3(50f, 5f, 0f);
            Debug.Log("Agent spawned on the new platform.");
        }
        else
        {
            agentPosition = new Vector3(-3.5f, 0f, 0f);
            Debug.Log("Agent spawned at the normal position.");
        }

        transform.position = agentPosition;

        if (isExtraGoalTraining)
        {
            targetTransform.position = new Vector3(0, -0.25f, 0);
        }
        else
        {
            targetTransform.position = new Vector3(5f, -0.25f, -4f);
        }

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

        // Re-enable all extra goals at episode start
        EnableAllExtraGoals();
    }

    private void EnableAllExtraGoals()
    {
        foreach (ExtraGoal goal in extraGoals)
        {
            if (goal != null)
            {
                goal.gameObject.SetActive(true);
            }
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
            if (!isExtraGoalTraining)
            {
                float explorationBonus = Academy.Instance.EnvironmentParameters.GetWithDefault("curiosity_strength", 0.0f);
                SetReward(3.0f + explorationBonus);
                agentMeshRenderer.material = winMaterial;
                EnableAllExtraGoals();
                EndEpisode();
            }
        }

        if (other.TryGetComponent<ExtraGoal>(out ExtraGoal extraGoal))
        {
            SetReward(1.5f);
            agentMeshRenderer.material = winMaterial;
            extraGoalsCollected++;
            extraGoal.gameObject.SetActive(false);

            if (isExtraGoalTraining && extraGoalsCollected >= 3)
            {
                EnableAllExtraGoals();
                EndEpisode();
            }
        }

        if (other.TryGetComponent<Wall>(out Wall wall))
        {
            SetReward(-2f);
            agentMeshRenderer.material = loseMaterial;
            EnableAllExtraGoals();
            EndEpisode();
        }
    }

    private void Update()
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


    public void ManuallyTriggerEndEpisode()
    {
        EnableAllExtraGoals();
        EndEpisode();
    }
}
