using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

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

    private bool isNewPlatformActive = false; // Track if the new platform is active

    private void Start()
    {
        agentRigidbody = GetComponent<Rigidbody>();

        // Lock X and Z rotation to prevent the agent from flipping
        agentRigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public override void OnEpisodeBegin()
    {
        cumulativeReward = 0f;
        stepCount = 0;

        float currentWallHeight = Academy.Instance.EnvironmentParameters.GetWithDefault("wall_height", 0.0f);

        // Check if the new platform is active
        isNewPlatformActive = currentWallHeight > 5.25f;

        Vector3 agentPosition;
        if (isNewPlatformActive)
        {
            // Spawn on the new platform at (50, 5, 0) in world space
            agentPosition = new Vector3(50f, 5f, 0f);
            Debug.Log("Agent spawned on the new platform at (50, 5, 0) in world space.");
        }
        else
        {
            // Spawn at the initial position (-3.5, 0, 0) in world space
            agentPosition = new Vector3(-3.5f, 0f, 0f);
            Debug.Log("Agent spawned at the initial position (-3.5, 0, 0) in world space.");
        }

        // Use world position for spawning
        transform.position = agentPosition;
        targetTransform.position = new Vector3(5f, -0.25f, -4f); // Fixed target position in world space

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

        // Debugging: Check for colliders at the spawn position
        Collider[] colliders = Physics.OverlapSphere(agentPosition, 0.1f);
        if (colliders.Length > 0)
        {
            Debug.LogWarning($"Colliders detected at spawn position: {agentPosition}");
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

        // Use Rigidbody for movement to respect physics and collisions
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
            canJumpOnJumpHelp = false;  // Reset JumpHelp after jumping
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
            // Increased reward for reaching the goal
            SetReward(+2f); // Increased from +1f to +2f
            agentMeshRenderer.material = winMaterial;
            EndEpisode();
        }

        if (other.TryGetComponent<ExtraGoal>(out ExtraGoal extraGoal))
        {
            // Reward for reaching the extra goal
            SetReward(+1.5f); // Adjust this value as needed
            agentMeshRenderer.material = winMaterial;
            EndEpisode();
        }

        if (other.TryGetComponent<Wall>(out Wall wall))
        {
            // Increased penalty for running out of bounds
            SetReward(-2f); // Increased from -1f to -2f
            agentMeshRenderer.material = loseMaterial;
            EndEpisode();
        }
    }

    void Update()
    {
        RotateTowardsMouse();

        float meanReward = stepCount > 0 ? cumulativeReward / stepCount : 0f;
        float currentWallHeight = Academy.Instance.EnvironmentParameters.GetWithDefault("wall_height", 0.0f);

        Debug.Log($"[INFO] Steps: {stepCount}, Mean Reward: {meanReward}, Wall Height: {currentWallHeight}, New Platform Active: {isNewPlatformActive}");
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
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (isNewPlatformActive)
        {
            // Visualize the new platform spawn position
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(new Vector3(50f, 5f, 0f), new Vector3(1f, 1f, 1f));
        }
        else
        {
            // Visualize the initial spawn position
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(new Vector3(-3.5f, 0f, 0f), new Vector3(1f, 1f, 1f));
        }
    }
}
