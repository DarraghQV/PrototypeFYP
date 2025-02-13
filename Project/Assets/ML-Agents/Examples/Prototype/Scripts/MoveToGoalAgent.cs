using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class MoveToGoalAgent : Agent
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

        Vector3 fixedTargetPosition = new Vector3(5f, -0.25f, -4f);

        float minX = 2.5f, maxX = 5f;
        float minZ = -3f, maxZ = 3f;
        float minDistance = 4f;

        Vector3 agentPosition;
        do
        {
            agentPosition = new Vector3(Random.Range(minX, maxX), 0, Random.Range(minZ, maxZ));
        }
        while (Vector3.Distance(agentPosition, fixedTargetPosition) < minDistance);

        transform.localPosition = agentPosition;
        targetTransform.localPosition = fixedTargetPosition;

        float currentWallHeight = Academy.Instance.EnvironmentParameters.GetWithDefault("wall_height", 0.0f);
        if (currentWallHeight > 0)
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

        float moveSpeed = 10f;
        transform.localPosition += new Vector3(moveX, 0, moveZ) * Time.deltaTime * moveSpeed;

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

        if (collision.collider.CompareTag("JumpHelp"))
        {
            canJumpOnJumpHelp = true;  // Allow jump when on JumpHelp tag
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider.CompareTag("JumpHelp"))
        {
            canJumpOnJumpHelp = false;  // Disallow jump when leaving JumpHelp
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<Goal>(out Goal goal))
        {
            SetReward(+1f);
            agentMeshRenderer.material = winMaterial;
            EndEpisode();
        }

        if (other.TryGetComponent<Wall>(out Wall wall))
        {
            SetReward(-1f);
            agentMeshRenderer.material = loseMaterial;
            EndEpisode();
        }
    }

    void Update()
    {
        RotateTowardsMouse();

        float meanReward = stepCount > 0 ? cumulativeReward / stepCount : 0f;
        float currentWallHeight = Academy.Instance.EnvironmentParameters.GetWithDefault("wall_height", 0.0f);

        Debug.Log($"[INFO] Steps: {stepCount}, Mean Reward: {meanReward}, Wall Height: {currentWallHeight}");
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
}
