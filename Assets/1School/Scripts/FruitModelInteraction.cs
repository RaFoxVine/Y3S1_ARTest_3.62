using UnityEngine;

/// <summary>
/// 控制水果模型的触屏旋转交互
/// 挂载到 AppleModel 和 BananaModel 上
/// </summary>
public class FruitModelInteraction : MonoBehaviour
{
    [Header("旋转设置")]
    [SerializeField] private float rotationSensitivity = 0.5f;

    [Header("缩放设置（可选）")]
    [SerializeField] private bool enablePinchZoom = false;
    [SerializeField] private float scaleSensitivity = 0.001f;
    [SerializeField] private float minScale = 0.05f;
    [SerializeField] private float maxScale = 0.5f;

    private Quaternion initialRotation;
    private Vector3 initialScale;
    private bool isInitialized = false;

    void Start()
    {
        // 记录初始状态
        initialRotation = transform.localRotation;
        initialScale = transform.localScale;
        isInitialized = true;
    }

    void OnEnable()
    {
        // 模型显示时，重置旋转和缩放
        if (isInitialized)
        {
            transform.localRotation = initialRotation;
            transform.localScale = initialScale;
        }
    }

    void Update()
    {
        HandleRotation();

        if (enablePinchZoom)
        {
            HandlePinchZoom();
        }
    }

    void HandleRotation()
    {
#if UNITY_EDITOR
        // Editor 中用鼠标模拟（鼠标左键按住拖动）
        if (Input.GetMouseButton(0))
        {
            // GetAxis 返回的已经是平滑的增量值（范围约 -1 到 1）
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            // Editor 下需要更高的灵敏度倍数（因为鼠标移动快）
            float editorSensitivityMultiplier = 5f;

            transform.Rotate(Vector3.up, -mouseX * rotationSensitivity * editorSensitivityMultiplier, Space.Self);
            transform.Rotate(Vector3.right, mouseY * rotationSensitivity * editorSensitivityMultiplier, Space.Self);
        }
#else
        // 真机上用触摸输入
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            
            // 只在手指移动时响应
            if (touch.phase == TouchPhase.Moved)
            {
                // deltaPosition 单位是像素，通常范围在几个到几十个像素
                float deltaX = touch.deltaPosition.x;
                float deltaY = touch.deltaPosition.y;
                
                // 左右滑动 → 水平旋转（绕 Y 轴）
                // 注意负号：向右滑动（deltaX > 0）应该让模型向左转（rotationY < 0）
                transform.Rotate(Vector3.up, -deltaX * rotationSensitivity, Space.Self);
                
                // 上下滑动 → 垂直旋转（绕 X 轴）
                // 向上滑动（deltaY > 0）应该让模型向下转（rotationX > 0）
                transform.Rotate(Vector3.right, deltaY * rotationSensitivity, Space.Self);
            }
        }
#endif
    }

    void HandlePinchZoom()
    {
        if (Input.touchCount == 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            // 只在两个手指都在移动时才执行缩放（避免刚放上第二个手指时跳变）
            if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
            {
                // 计算上一帧的两指距离
                Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
                Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;
                float previousDistance = Vector2.Distance(touch0PrevPos, touch1PrevPos);

                // 计算当前两指距离
                float currentDistance = Vector2.Distance(touch0.position, touch1.position);

                // 距离变化量（像素）
                float deltaDistance = currentDistance - previousDistance;

                // 应用缩放
                Vector3 newScale = transform.localScale * (1f + deltaDistance * scaleSensitivity);

                // 限制缩放范围
                newScale.x = Mathf.Clamp(newScale.x, minScale, maxScale);
                newScale.y = Mathf.Clamp(newScale.y, minScale, maxScale);
                newScale.z = Mathf.Clamp(newScale.z, minScale, maxScale);

                transform.localScale = newScale;
            }
        }
    }
}