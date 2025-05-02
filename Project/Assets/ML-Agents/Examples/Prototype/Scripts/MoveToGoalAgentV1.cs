using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using System.Collections.Generic;

public class MoveToGoalAgentV1 : Agent
{
    [Header("Object References")]
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Material winMaterial;
    [SerializeField] private Material loseMaterial;
    [SerializeField] private MeshRenderer[] agentMeshRenderers = new MeshRenderer[3]; 
    [SerializeField] private Transform mainCameraTransform;
    [SerializeField] private GameObject wallPrefab;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpVelocity = 6f;

    [Header("Reward Settings")]
    [SerializeField] private float mainGoalOnlyReward = 2.5f;
    [SerializeField] private float extraGoalReward = 2.0f;
    [SerializeField] private float combinedGoalRewardBonus = 6.0f;
    [SerializeField] private float standardPenalty = -1.0f;
    [SerializeField] private float penaltyAfterExtraGoal = -2.0f;
    [SerializeField] private float stepPenalty = -0.001f;

    private Rigidbody agentRigidbody;
    private bool isGrounded;
    private GameObject wallInstance;
    private List<ExtraGoal> extraGoals = new List<ExtraGoal>();

    private int stepCount = 0;
    private int extraGoalsCollected = 0;
    private bool isExtraGoalTraining = false;
    private bool isNewPlatformActive = false;
    private bool hasCollectedExtraGoalThisEpisode = false;

    private void Start()
    {
        agentRigidbody = GetComponent<Rigidbody>();
        agentRigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        CacheExtraGoals();
        if (mainCameraTransform == null) { Debug.LogWarning("Main Camera Transform not assigned..."); }

        bool allRenderersAssigned = true;
        if (agentMeshRenderers == null || agentMeshRenderers.Length != 3) {
            allRenderersAssigned = false;
        } else {
            foreach(MeshRenderer rend in agentMeshRenderers) {
                if (rend == null) {
                    allRenderersAssigned = false;
                    break;
                }
            }
        }
        if (!allRenderersAssigned) {
             Debug.LogWarning("Agent Mesh Renderers array not fully assigned in Inspector! Material changes might fail.");
        }
    }

    private void CacheExtraGoals()
    {
        ExtraGoal[] allExtraGoals = Resources.FindObjectsOfTypeAll<ExtraGoal>();
        extraGoals.Clear();
        foreach (ExtraGoal goal in allExtraGoals)
        {
            if (goal.gameObject.scene.IsValid()) { extraGoals.Add(goal); }
        }
    }

    public override void OnEpisodeBegin()
    {
        stepCount = 0;
        extraGoalsCollected = 0;
        hasCollectedExtraGoalThisEpisode = false;

        float currentWallHeight = Academy.Instance.EnvironmentParameters.GetWithDefault("wall_height", 0.0f);
        isExtraGoalTraining = Mathf.Approximately(currentWallHeight, -1.0f);
        isNewPlatformActive = Mathf.Approximately(currentWallHeight, 5.3f);

        Vector3 agentPosition;
        Vector3 cameraTargetPosition = mainCameraTransform != null ? mainCameraTransform.position : Vector3.zero;

        if (isExtraGoalTraining)
        {
            agentPosition = new Vector3(-41f, 0.5f, 0f);
            cameraTargetPosition = new Vector3(-40f, 30f, 35f);
        }
        else if (isNewPlatformActive)
        {
            agentPosition = new Vector3(50f, 5.5f, 0f);
            cameraTargetPosition = new Vector3(60f, 30f, 35f);
        }
        else
        {
            agentPosition = new Vector3(-3.5f, 0.5f, 0f);
            cameraTargetPosition = new Vector3(0f, 30f, 35f);
        }

        transform.localPosition = agentPosition;
        agentRigidbody.velocity = Vector3.zero;
        agentRigidbody.angularVelocity = Vector3.zero;

        if (mainCameraTransform != null) {
            mainCameraTransform.position = cameraTargetPosition;
        }

        if (isExtraGoalTraining) {
            targetTransform.localPosition = new Vector3(1000f, -100f, 1000f);
        }

        else if (isNewPlatformActive) {
            targetTransform.localPosition = new Vector3(70f, 3f, 0f);
        }
        
        else {
            targetTransform.localPosition = new Vector3(5f, 1.5f, -4f);
        }

        bool shouldWallBeActive = currentWallHeight > 0 && !isExtraGoalTraining && !isNewPlatformActive;
        if (shouldWallBeActive)
        {
            if (wallInstance == null) { wallInstance = Instantiate(wallPrefab, transform.parent); }
            wallInstance.transform.localPosition = new Vector3(0, currentWallHeight / 2f, 0);
            wallInstance.transform.localScale = new Vector3(1f, currentWallHeight, 15f);
            wallInstance.SetActive(true);
        }
        else { if (wallInstance != null) { wallInstance.SetActive(false); } }

        EnableAllExtraGoals();
        isGrounded = false;
    }

    private void EnableAllExtraGoals()
    {
        foreach (ExtraGoal goal in extraGoals)
        {
            if (goal != null && goal.gameObject != null) { goal.gameObject.SetActive(true); }
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(isGrounded);
        sensor.AddObservation(hasCollectedExtraGoalThisEpisode);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        stepCount++;
        float currentStepPenalty = isNewPlatformActive ? 0f : stepPenalty;
        if (currentStepPenalty != 0) { AddReward(currentStepPenalty); }

        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        int jumpAction = actions.DiscreteActions[0];

        Vector3 movement = new Vector3(moveX, 0, moveZ) * moveSpeed * Time.deltaTime;
        Vector3 targetPosition = agentRigidbody.position + movement;
        agentRigidbody.MovePosition(targetPosition);

        bool jumpAllowed = !isExtraGoalTraining;
        if (jumpAction == 1 && jumpAllowed && isGrounded) { Jump(jumpVelocity); }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Horizontal");
        continuousActions[1] = Input.GetAxisRaw("Vertical");
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }

    private void Jump(float velocity)
    {
        agentRigidbody.AddForce(Vector3.up * velocity, ForceMode.VelocityChange);
        isGrounded = false;
    }

    private void OnCollisionEnter(Collision collision) {
        if (collision.gameObject.CompareTag("ground")) {
            isGrounded = true;
        }
    }

    private void OnCollisionStay(Collision collision) {
        if (collision.gameObject.CompareTag("ground")) {
            isGrounded = true;
        }
    }

    private void OnCollisionExit(Collision collision) {
        if (collision.gameObject.CompareTag("ground")) {
            isGrounded = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("goal"))
        {
            if (!isExtraGoalTraining)
            {
                float finalReward = mainGoalOnlyReward;
                if (hasCollectedExtraGoalThisEpisode)
                {
                    finalReward += combinedGoalRewardBonus;
                    Debug.Log($"Main Goal Hit: COMBINED Reward! Total: {finalReward}");
                }
                else
                {
                    Debug.Log($"Main Goal Hit: Main Goal ONLY Reward ({finalReward})");
                }
                SetReward(finalReward);
                SetAgentMaterials(winMaterial);
                EndEpisode();
            }
        }

        if (other.CompareTag("ExtraGoal"))
        {
            if (other.gameObject.activeSelf)
            {
                SetReward(extraGoalReward);
                Debug.Log($"Extra Goal Hit: Reward ({extraGoalReward})");
                SetAgentMaterials(winMaterial);
                extraGoalsCollected++;
                hasCollectedExtraGoalThisEpisode = true;
                other.gameObject.SetActive(false);
                if (isExtraGoalTraining && extraGoalsCollected >= 3) { EndEpisode(); }
            }
        }

        if (other.CompareTag("wall") || other.CompareTag("boundary"))
        {
            float penalty;
            if (isExtraGoalTraining || !hasCollectedExtraGoalThisEpisode)
            {
                penalty = standardPenalty;
                Debug.Log($"Wall/Boundary Hit: STANDARD Penalty ({penalty}). Reason: ExtraGoalTr={isExtraGoalTraining}, HasCollectedExtra={hasCollectedExtraGoalThisEpisode}");
            }
            else
            {
                penalty = penaltyAfterExtraGoal;
                Debug.Log($"Wall/Boundary Hit: HARSHER Penalty ({penalty})");
            }
            SetReward(penalty);
            SetAgentMaterials(loseMaterial);
            EndEpisode();
        }
    }

    private void SetAgentMaterials(Material mat)
    {
        if (agentMeshRenderers == null) return; 

        foreach(MeshRenderer rend in agentMeshRenderers)
        {
            if (rend != null) 
            {
                rend.material = mat;
            }
        }
    }

    public void ManuallyTriggerEndEpisode() { EndEpisode(); }
}
