using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;
using static UnityEngine.XR.ARSubsystems.XRCpuImage;
using TMPro;

using Unity.Collections;  // 用于 NativeArray
using UnityEngine.XR.ARSubsystems;  // 用于 XRCpuImage

/// <summary>
/// 水果检测主管理器
/// 功能：截取 AR 相机画面 → 发送到服务端 → 显示检测框和 3D 模型
/// </summary>
public class FruitDetectionManager : MonoBehaviour
{
    [Header("AR 组件")]
    [SerializeField] private ARCameraManager arCameraManager;

    [Header("服务端配置")]
    [SerializeField] private string serverUrl = "http://172.22.105.238:5000/detect";

    [Header("UI 组件")]
    [SerializeField] private Transform detectionBoxContainer;
    [SerializeField] private GameObject detectionBoxPrefab;

    [Header("3D 模型")]
    [SerializeField] private GameObject appleModel;   // 改为直接引用场景中的物体
    [SerializeField] private GameObject bananaModel;  // 改为直接引用场景中的物体

    [Header("营养信息")]
    [SerializeField] private NutritionDisplayManager nutritionDisplay;

    [Header("调试")]
    [SerializeField] private float captureInterval = 1f;
    [SerializeField] private bool showDebugInfo = true;

    // 运行时变量
    private Texture2D captureTexture;
    private float lastCaptureTime;
    private List<GameObject> currentBoxes = new List<GameObject>();
    private bool isProcessing = false;
    private ARCameraBackground cameraBackground;

    // 相机分辨率
    private const int CAPTURE_WIDTH = 640;
    private const int CAPTURE_HEIGHT = 480;

    [Header("调试 UI")]
    [SerializeField] private TextMeshProUGUI debugLogText;  // 如果用 TextMeshPro，改成 TextMeshProUGUI
    [SerializeField] private int maxLogLines = 15; // 最多显示多少行

    private List<string> logMessages = new List<string>();

    void Start()
    {
        // 创建用于截图的纹理
        captureTexture = new Texture2D(CAPTURE_WIDTH, CAPTURE_HEIGHT, TextureFormat.RGB24, false);

        // 获取 ARCameraBackground 组件
        cameraBackground = arCameraManager.GetComponent<ARCameraBackground>();

        // 验证模型引用
        if (appleModel == null)
        {
            AddLog("[WARN] AppleModel 未分配");
        }
        else
        {
            appleModel.SetActive(false);  // 初始隐藏
        }

        if (bananaModel == null)
        {
            AddLog("[WARN] BananaModel 未分配");
        }
        else
        {
            bananaModel.SetActive(false);  // 初始隐藏
        }

        // 验证必要组件
        if (arCameraManager == null)
        {
            Debug.LogError("[FruitDetection] ARCameraManager 未分配！请在 Inspector 中拖入 Main Camera");
            enabled = false;
            return;
        }

        if (cameraBackground == null)
        {
            Debug.LogError("[FruitDetection] ARCameraBackground 组件未找到！");
            enabled = false;
            return;
        }

        if (detectionBoxContainer == null)
        {
            Debug.LogError("[FruitDetection] DetectionBoxContainer 未分配！");
            enabled = false;
            return;
        }

        if (detectionBoxPrefab == null)
        {
            Debug.LogError("[FruitDetection] DetectionBoxPrefab 未分配！");
            enabled = false;
            return;
        }

        AddLog($"URL:'{serverUrl}'");
        AddLog($"URL Length {serverUrl.Length}");
        AddLog($"URL First Character ASCII: {(int)serverUrl[0]}");

        AddLog($"[FruitDetection] 初始化完成");
        AddLog($"[FruitDetection] 服务端地址: {serverUrl}");
        AddLog($"[FruitDetection] 截图间隔: {captureInterval}秒");
        AddLog($"[FruitDetection] 截图分辨率: {CAPTURE_WIDTH}x{CAPTURE_HEIGHT}");

        lastCaptureTime = Time.time + 1f;
    }

    void Update()
    {
        if (Time.frameCount % 60 == 0) // 每 60 帧（约 1 秒）打印一次
        {
            AddLog($"Update isProcessing: {isProcessing}  --  {Time.time - lastCaptureTime:F1}second");
        }

        if (Time.time - lastCaptureTime >= captureInterval && !isProcessing)
        {
            AddLog("Send New Capture");
            lastCaptureTime = Time.time;
            StartCoroutine(CaptureAndDetect());
        }
    }


    //bool needsRotation = false;
    IEnumerator CaptureAndDetect()
    {
        isProcessing = true;

        // 1. 尝试获取 CPU 侧的相机图像
        if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage))
        {
            if (showDebugInfo)
                AddLog("[WARN] 无法获取相机图像");
            isProcessing = false;
            yield break;
        }

        AddLog($"获取到相机图像: {cpuImage.width}x{cpuImage.height}, 格式: {cpuImage.format}");

        //// 检查图像是否需要旋转
        //needsRotation = false;
        //bool isPortrait = Screen.height > Screen.width;
        //bool imageIsLandscape = cpuImage.width > cpuImage.height;

        //if (isPortrait && imageIsLandscape)
        //{
        //    AddLog("[WARN] 检测到旋转：手机竖屏，但相机图像是横向的");
        //    needsRotation = true;
        //}

        // 2. 设置转换参数：转为 RGB24 格式
        var conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, cpuImage.width, cpuImage.height),
            outputDimensions = new Vector2Int(CAPTURE_WIDTH, CAPTURE_HEIGHT),
            outputFormat = TextureFormat.RGB24,
            transformation = XRCpuImage.Transformation.MirrorY  // 翻转 Y 轴
        };

        // 3. 计算需要的缓冲区大小
        int size = cpuImage.GetConvertedDataSize(conversionParams);
        var buffer = new NativeArray<byte>(size, Allocator.Temp);

        try
        {
            // 4. 执行转换
            cpuImage.Convert(conversionParams, buffer);

            // 5. 将数据复制到 Texture2D
            if (captureTexture == null || captureTexture.width != CAPTURE_WIDTH || captureTexture.height != CAPTURE_HEIGHT)
            {
                if (captureTexture != null)
                    Destroy(captureTexture);
                captureTexture = new Texture2D(CAPTURE_WIDTH, CAPTURE_HEIGHT, TextureFormat.RGB24, false);
            }

            captureTexture.LoadRawTextureData(buffer);
            captureTexture.Apply();

            AddLog($"图像转换完成: {CAPTURE_WIDTH}x{CAPTURE_HEIGHT}");

            // 6. 压缩为 JPEG
            byte[] jpegData = ImageConversion.EncodeToJPG(captureTexture, 75);

            if (showDebugInfo)
                AddLog($"截图完成: {jpegData.Length / 1024}KB");

            // 7. 发送到服务端
            using (UnityWebRequest request = new UnityWebRequest(serverUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(jpegData);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "image/jpeg");
                request.timeout = 5;

                AddLog("发送请求到服务端...");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    AddLog($"[ERROR] 请求失败: {request.error}");
                    AddLog($"[ERROR] Code: {request.responseCode}");
                    isProcessing = false;
                    buffer.Dispose();
                    cpuImage.Dispose();
                    yield break;
                }

                string jsonResponse = request.downloadHandler.text;

                if (showDebugInfo)
                    AddLog($"收到响应: {jsonResponse}");

                try
                {
                    DetectionResponse response = JsonUtility.FromJson<DetectionResponse>(jsonResponse);

                    if (!response.success)
                    {
                        AddLog($"[ERROR] 服务端错误: {response.error}");
                        isProcessing = false;
                        buffer.Dispose();
                        cpuImage.Dispose();
                        yield break;
                    }

                    UpdateDetectionUI(response.detections);
                }
                catch (Exception e)
                {
                    AddLog($"[ERROR] JSON 解析失败: {e.Message}");
                }
            }
        }
        finally
        {
            // 8. 释放资源（必须！）
            buffer.Dispose();
            cpuImage.Dispose();
        }

        isProcessing = false;
    }

    void UpdateDetectionUI(Detection[] detections)
    {
        // 清除旧的检测框
        foreach (var box in currentBoxes)
        {
            Destroy(box);
        }
        currentBoxes.Clear();

        // 先隐藏所有模型
        if (appleModel != null)
            appleModel.SetActive(false);
        if (bananaModel != null)
            bananaModel.SetActive(false);

        // 如果没有检测结果，直接返回
        if (detections == null || detections.Length == 0)
        {
            if (showDebugInfo)
                AddLog("未检测到水果");

            // 隐藏营养信息
            if (nutritionDisplay != null)
                nutritionDisplay.HideNutrition();

            return;
        }

        // 取置信度最高的检测结果
        Detection bestDetection = detections[0];
        foreach (var det in detections)
        {
            if (det.confidence > bestDetection.confidence)
                bestDetection = det;
        }

        AddLog($"检测到: {bestDetection.label} (置信度: {bestDetection.confidence:F2})");

        // 绘制检测框
        DrawDetectionBox(bestDetection);

        // 显示对应的 3D 模型
        ShowFruitModel(bestDetection.label);

        if (nutritionDisplay != null)
        {
            nutritionDisplay.ShowNutrition(bestDetection.label);
        }
    }
    /// <summary>
    /// 绘制检测框
    /// </summary>
    void DrawDetectionBox(Detection detection)
    {
        GameObject boxObj = Instantiate(detectionBoxPrefab, detectionBoxContainer);
        RectTransform boxRect = boxObj.GetComponent<RectTransform>();

        if (boxRect == null)
        {
            AddLog("[ERROR] DetectionBoxPrefab 缺少 RectTransform！");
            Destroy(boxObj);
            return;
        }

        // === 诊断信息 ===
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        AddLog($"Screen Resolution {screenWidth}x{screenHeight}");
        AddLog($"Origin box: [{detection.box[0]}, {detection.box[1]}, {detection.box[2]}, {detection.box[3]}]");

        // === 关键修改：检查是否需要旋转坐标 ===
        bool isPortrait = screenHeight > screenWidth;
        bool imageIsLandscape = CAPTURE_WIDTH > CAPTURE_HEIGHT;  // 640 > 480，是横向

        float x1, y1, x2, y2;

        if (isPortrait && imageIsLandscape)
        {
            // 图像被旋转了 90° 顺时针
            // 需要反向旋转坐标
            AddLog("Apply Rotation");

            // 原始坐标（图像坐标系）
            int origX1 = detection.box[0];
            int origY1 = detection.box[1];
            int origX2 = detection.box[2];
            int origY2 = detection.box[3];

            // === 修正：逆时针旋转 90° ===
            // 变换公式：(x, y) → (HEIGHT - y, x)
            // 其中 HEIGHT 是图像高度（480）
            x1 = CAPTURE_HEIGHT - origY2;  // 480 - y2
            y1 = origX1;
            x2 = CAPTURE_HEIGHT - origY1;  // 480 - y1
            y2 = origX2;

            // 缩放到屏幕分辨率（注意：宽高对调）
            float scaleX = screenWidth / (float)CAPTURE_HEIGHT;   // 屏幕宽 / 图像高
            float scaleY = screenHeight / (float)CAPTURE_WIDTH;   // 屏幕高 / 图像宽

            x1 *= scaleX;
            y1 *= scaleY;
            x2 *= scaleX;
            y2 *= scaleY;


        }
        else
        {
            // 不需要旋转，正常处理
            AddLog("No need Rotation");

            float scaleX = screenWidth / (float)CAPTURE_WIDTH;
            float scaleY = screenHeight / (float)CAPTURE_HEIGHT;

            x1 = detection.box[0] * scaleX;
            y1 = detection.box[1] * scaleY;
            x2 = detection.box[2] * scaleX;
            y2 = detection.box[3] * scaleY;
        }

        // === 反向修正（因为上下左右反了）===
        // 翻转 X 轴
        float tempX1 = screenWidth - x1;
        float tempX2 = screenWidth - x2;
        x1 = Mathf.Min(tempX1, tempX2);
        x2 = Mathf.Max(tempX1, tempX2);

        // 翻转 Y 轴
        float tempY1 = screenHeight - y1;
        float tempY2 = screenHeight - y2;
        y1 = Mathf.Min(tempY1, tempY2);
        y2 = Mathf.Max(tempY1, tempY2);

        //AddLog($"转换后 box: x1={x1:F0}, y1={y1:F0}, x2={x2:F0}, y2={y2:F0}");


        // 3. 计算中心点和尺寸
        float centerX = (x1 + x2) / 2f;
        float centerY_ImageSpace = (y1 + y2) / 2f;  // 图像坐标系（左上角原点）
        float width = x2 - x1;
        float height = y2 - y1;

        // 4. 转换到 UI 坐标系（左下角原点）
        float centerY_UISpace = screenHeight - centerY_ImageSpace;

        AddLog($"Center(UI): ({centerX:F0}, {centerY_UISpace:F0}), 尺寸: {width:F0}x{height:F0}");

        // === 设置 RectTransform ===
        // 锚点设置为左下角 (0, 0)
        boxRect.anchorMin = new Vector2(0, 0);
        boxRect.anchorMax = new Vector2(0, 0);
        boxRect.pivot = new Vector2(0.5f, 0.5f);

        // 设置位置和大小
        boxRect.anchoredPosition = new Vector2(centerX, centerY_UISpace);
        boxRect.sizeDelta = new Vector2(width, height);

        currentBoxes.Add(boxObj);

        //AddLog($"检测框已绘制");
    }

    /// <summary>
    /// 实例化水果 3D 模型
    /// </summary>
    /// <summary>
    /// 显示对应的水果模型
    /// </summary>
    void ShowFruitModel(string label)
    {
        if (label == "apple" && appleModel != null)
        {
            appleModel.SetActive(true);
            AddLog("已显示苹果模型");
        }
        else if (label == "banana" && bananaModel != null)
        {
            bananaModel.SetActive(true);
            AddLog("已显示香蕉模型");
        }
        else
        {
            if (showDebugInfo)
                AddLog($"[WARN] 未找到 {label} 的模型");
        }
    }

    string m_log;

    /// <summary>
    /// 添加日志到屏幕显示
    /// </summary>
    /// 
    void AddLog(string message)
    {
        string timeStamp = System.DateTime.Now.ToString("HH:mm:ss");
        string logLine = $"[{timeStamp}] {message}";

        logMessages.Add(logLine);

        // 限制日志行数
        if (logMessages.Count > maxLogLines)
        {
            logMessages.RemoveAt(0);
        }

        // 更新 UI
        if (debugLogText != null)
        {
            debugLogText.text = string.Join("\n", logMessages);
        }

        // 同时输出到 Console
        Debug.Log(message);
    }

    void OnDestroy()
    {
        if (captureTexture != null)
        {
            Destroy(captureTexture);
        }
    }
}

// ==================== JSON 数据结构 ====================

[Serializable]
public class DetectionResponse
{
    public bool success;
    public string error;
    public Detection[] detections;
}

[Serializable]
public class Detection
{
    public string label;
    public float confidence;
    public int[] box; // [x1, y1, x2, y2]
}
