using UnityEngine;

public class MovingSphere : MonoBehaviour
{
    [SerializeField, Range(0f, 100f)]
    float maxSpeed = 10f; // 最大速度
    [SerializeField, Range(0f, 100f)]
    float maxAcceleration = 10f; // 最大加速度
    [SerializeField, Range(0f, 100f)]
    float maxAirAcceleration = 1f; // 空中最大加速度
    [SerializeField, Range(0f, 10f)]
    float jumpHeight = 2f; // 跳跃高度
    [SerializeField, Range(0, 5)]
    int maxAirJumps = 0; // 空中跳跃
    [SerializeField, Range(0f, 90f)]
    float maxGroundAngle = 25f; // 最大地面角

    bool desiredJump; // 是否跳跃
    int groundContactCount; // 接触地面的数量
    bool OnGround => groundContactCount > 0;
    int jumpPhase; // 跳跃阶段
    Vector3 velocity, desiredVelocity;
    float minGroundDotProduct; // 最大地面角法线的y分量
    Vector3 contactNormal; // 接触点法线
    Rigidbody body;


    void Awake()
    {
        body = GetComponent<Rigidbody>();
        OnValidate();
    }

    void OnValidate()
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
    }

    void Update()
    {
        // 获取输入
        Vector2 playerInput;
        playerInput.x = Input.GetAxis("Horizontal");
        playerInput.y = Input.GetAxis("Vertical");
        playerInput = Vector2.ClampMagnitude(playerInput, 1f);

        desiredVelocity = new Vector3(playerInput.x, 0f, playerInput.y) * maxSpeed;
        desiredJump |= Input.GetButtonDown("Jump");


        GetComponent<Renderer>().material.SetColor("_Color", Color.white * (groundContactCount * 0.25f));
    }

    private void FixedUpdate()
    {
        // 移动
        UpdateState();
        AdjustVelocity();

        // 跳跃
        if (desiredJump)
        {
            desiredJump = false;
            Jump();
        }
        body.velocity = velocity;
        ClearState(); // 地面接触状态
    }

    void ClearState()
    {
        groundContactCount = 0;
        contactNormal = Vector3.zero;
    }

    void UpdateState()
    {
        velocity = body.velocity;
        if (OnGround)
        {
            // 重置跳跃阶段
            jumpPhase = 0;
            if (groundContactCount > 1)
            {
                contactNormal.Normalize();
            }
        }
        else
        {
            // 重置接触点法线
            contactNormal = Vector3.up;
        }
    }

    void Jump()
    {
        if (OnGround || jumpPhase < maxAirJumps)
        {
            jumpPhase += 1;
            float jumpSpeed = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
            float alignedSpeed = Vector3.Dot(velocity, contactNormal);
            // 限制上跳速度
            if (alignedSpeed > 0f)
            {
                jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
            }
            // 沿法线方向跳跃
            velocity += contactNormal * jumpSpeed;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        EvaluateCollision(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        EvaluateCollision(collision);
    }

    // 计算碰撞接触点的信息
    void EvaluateCollision(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            // 只要有一个接触点的法线大于阈值，就可以跳跃
            if (normal.y >= minGroundDotProduct)
            {
                groundContactCount += 1;
                contactNormal += normal;
            }
        }
    }

    // 沿着接触面切向的分量
    Vector3 ProjectOnContactPlane(Vector3 vector)
    {
        return vector - contactNormal * Vector3.Dot(vector, contactNormal);
    }

    // 调整速度
    void AdjustVelocity()
    {
        // 将x，z轴投影到斜面
        Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
        Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;

        // 计算当前速度沿投影坐标轴的分量
        float currentX = Vector3.Dot(velocity, xAxis);
        float currentZ = Vector3.Dot(velocity, zAxis);

        // 更新速度
        float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
        float maxSpeedChange = acceleration * Time.deltaTime;

        float newX = Mathf.MoveTowards(currentX, desiredVelocity.x, maxSpeedChange);
        float newZ = Mathf.MoveTowards(currentZ, desiredVelocity.z, maxSpeedChange);

        velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
    }
}