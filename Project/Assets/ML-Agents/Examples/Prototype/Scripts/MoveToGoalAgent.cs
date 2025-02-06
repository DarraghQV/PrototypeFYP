using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents;
using UnityEngine;

public class MoveToGoalAgent : Agent
{
    [SerializeField] private Transform targetTransform;  // Add this line to reference the target/goal
    [SerializeField] private Material winMaterial;
    [SerializeField] private Material loseMaterial;
    [SerializeField] private MeshRenderer agentMeshRenderer;

    private Rigidbody agentRigidbody;
    [SerializeField] private float jumpForce = 5f;
    private bool isGrounded;

    private GameObject wallInstance;
    [SerializeField] private GameObject wallPrefab;

    // The wall heights will be updated automatically based on curriculum.
    private float[] wallHeights = new float[] { 1f, 3f, 5f, 7f };  // Different wall heights for curriculum

    private void Start()
    {
        agentRigidbody = GetComponent<Rigidbody>();
    }

    public override void OnEpisodeBegin()
    {
        // Define platform boundaries and random positions
        float minX = 2.5f, maxX = 5f;
        float minZ = -3f, maxZ = 3f;

        // Generate random positions ensuring a minimum distance between them
        Vector3 agentPosition, targetPosition;
        float minDistance = 4f;

        do
        {
            agentPosition = new Vector3(Random.Range(minX, maxX), 0, Random.Range(minZ, maxZ));
            targetPosition = new Vector3(Random.Range(minX, maxX), 0, Random.Range(minZ, maxZ));
        }
        while (Vector3.Distance(agentPosition, targetPosition) < minDistance);

        // Assign positions
        transform.localPosition = agentPosition;
        targetTransform.localPosition = targetPosition;  // Now it will work, as targetTransform is assigned

        // Spawn the wall
        if (wallInstance == null)
        {
            wallInstance = Instantiate(wallPrefab);
        }

        // Set wall position in the center
        wallInstance.transform.position = new Vector3(0, 0.5f, 0);

        // Wall height is automatically adjusted by curriculum, but we can start with the lowest value
        wallInstance.transform.localScale = new Vector3(1f, wallHeights[0], 15f);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(targetTransform.localPosition);  // Use targetTransform here
        sensor.AddObservation(isGrounded);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        int jumpAction = actions.DiscreteActions[0];

        // Move the agent
        float moveSpeed = 10f;
        transform.localPosition += new Vector3(moveX, 0, moveZ) * Time.deltaTime * moveSpeed;

        // Handle jumping
        if (jumpAction == 1 && isGrounded) Jump();
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
        if (isGrounded)
        {
            agentRigidbody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
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
            SetReward(+1f); // Agent earns reward for reaching the goal
            agentMeshRenderer.material = winMaterial;
            EndEpisode();
        }

        if (other.TryGetComponent<Wall>(out Wall wall))
        {
            SetReward(-1f); // Agent earns negative reward for hitting the wall
            agentMeshRenderer.material = loseMaterial;
            EndEpisode();
        }
    }
}
