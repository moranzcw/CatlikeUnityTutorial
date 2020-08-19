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
    [SerializeField, Range(0f, 100f)]
    float maxSnapSpeed = 100f; // 最大地面捕捉速度
    [SerializeField, Min(0f)]
    float probeDistance = 1f; // 探测距离

    bool desiredJump; // 是否跳跃
    int groundContactCount; // 接触地面的数量
    bool OnGround => groundContactCount > 0;
    int jumpPhase; // 跳跃阶段
    Vector3 velocity, desiredVelocity;
    float minGroundDotProduct; // 最大地面角法线的y分量
    Vector3 contactNormal; // 接触点法线
    int stepsSinceLastGrounded; // 离开地面之后的物理帧
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

        // 显示接触状态
        //GetComponent<Renderer>().material.SetColor("_Color", Color.white * (groundContactCount * 0.25f));
        GetComponent<Renderer>().material.SetColor("_Color", OnGround ? Color.black : Color.white);
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

    // 清空状态
    void ClearState()
    {
        groundContactCount = 0;
        contactNormal = Vector3.zero;
    }

    // 更新状态
    void UpdateState()
    {
        stepsSinceLastGrounded += 1;
        velocity = body.velocity;
        if (OnGround || SnapToGround())
        {
            stepsSinceLastGrounded = 0;
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

    // 跳跃
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

    // 碰撞体进入
    void OnCollisionEnter(Collision collision)
    {
        EvaluateCollision(collision);
    }


    // 碰撞体保持
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

    // 捕捉到地面
    bool SnapToGround()
    {
        if (stepsSinceLastGrounded > 1)
        {   // 仅在离开地面后第一个物理帧扑捉一次
            return false;
        }
        float speed = velocity.magnitude; // 速度的标量
        if (speed > maxSnapSpeed)
        {
            return false;
        }
        if (!Physics.Raycast(body.position, Vector3.down, out RaycastHit hit, probeDistance))
        {
            // 没有地面交点
            return false;
        }
        if (hit.normal.y < minGroundDotProduct)
        {
            // 地面交点角度太大
            return false;
        }
        groundContactCount = 1;
        contactNormal = hit.normal;

        float dot = Vector3.Dot(velocity, hit.normal); // 速度在法线上的投影
        if (dot > 0f)
        {
            // 保持速度标量不变，调整方向到切向
            velocity = (velocity - hit.normal * dot).normalized * speed;
        }
        return true;
    }
}