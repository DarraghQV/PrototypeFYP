using System.Collections;
using System.Collections.Generic;
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

    // Add a reference to the Rigidbody for jumping
    private Rigidbody agentRigidbody;
    [SerializeField] private float jumpForce = 5f;
    private bool isGrounded;

    // Called at the start to initialize the Rigidbody
    private void Start()
    {
        agentRigidbody = GetComponent<Rigidbody>();
    }

    [SerializeField] private GameObject wallPrefab;
    private GameObject wallInstance;

    public override void OnEpisodeBegin()
    {
        // Define platform boundaries
        float minX = -2f, maxX = 2f;
        float minZ = -2f, maxZ = 2f;

        // Generate random positions ensuring a minimum distance between them
        Vector3 agentPosition, targetPosition;
        float minDistance = 4f; // Ensure they are not too close

        do
        {
            agentPosition = new Vector3(Random.Range(minX, maxX), 0, Random.Range(minZ, maxZ));
            targetPosition = new Vector3(Random.Range(minX, maxX), 0, Random.Range(minZ, maxZ));
        }
        while (Vector3.Distance(agentPosition, targetPosition) < minDistance);

        // Assign positions
        transform.localPosition = agentPosition;
        targetTransform.localPosition = targetPosition;

        // Spawn the wall once per episode in the middle of the platform
        if (wallInstance == null)
        {
            wallInstance = Instantiate(wallPrefab);
        }

        // Set wall position in the center
        wallInstance.transform.position = new Vector3(0, 0.5f, 0);

        // Set wall height randomly and ensure it spans the platform (Z scale = 15)
        float wallHeight = Random.Range(1f, 2.5f);
        wallInstance.transform.localScale = new Vector3(1f, wallHeight, 15f);
    }



    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(targetTransform.localPosition);
        sensor.AddObservation(isGrounded); // Add whether the agent is grounded
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        int jumpAction = actions.DiscreteActions[0]; // Get the jump action

        float moveSpeed = 10f;
        transform.localPosition += new Vector3(moveX, 0, moveZ) * Time.deltaTime * moveSpeed;

        // Handle jumping action
        if (jumpAction == 1 && isGrounded) // Check if the agent should jump
        {
            Jump();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Horizontal");
        continuousActions[1] = Input.GetAxisRaw("Vertical");

        // For jump action, use the spacebar as an input
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = Input.GetKey(KeyCode.Space) ? 1 : 0; // 1 for jump, 0 for no jump
    }

    private void Jump()
    {
        // Apply a force to the Rigidbody to make the agent jump
        if (isGrounded)
        {
            agentRigidbody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false; // Set isGrounded to false while in the air
        }
    }

    // Check if the agent is grounded by detecting collisions
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("ground")) // Assuming "Ground" is the tag for the ground
        {
            isGrounded = true;
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
}
