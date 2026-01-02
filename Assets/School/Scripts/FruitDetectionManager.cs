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

    [Header("3D Models")]
    [SerializeField] private GameObject appleModel;   // 手动拖拽 Prefab 引用
    [SerializeField] private GameObject bananaModel;  // 手动拖拽 Prefab 引用
    [SerializeField] private GameObject orangeModel;  // 手动拖拽 Prefab 引用



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

    private string currentFruitLabel = "";  // 记录当前显示的水果类型

    void Start()
    {
        // 创建用于截图的纹理
        captureTexture = new Texture2D(CAPTURE_WIDTH, CAPTURE_HEIGHT, TextureFormat.RGB24, false);

        // 获取 ARCameraBackground 组件
        cameraBackground = arCameraManager.GetComponent<ARCameraBackground>();


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
                AddLog("[WARN] Cant get Camera Image");
            isProcessing = false;
            yield break;
        }

        //AddLog($"获取到相机图像: {cpuImage.width}x{cpuImage.height}, 格式: {cpuImage.format}");

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

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    AddLog($"[ERROR] Request Fail: {request.error}");
                    AddLog($"[ERROR] Code: {request.responseCode}");
                    isProcessing = false;
                    buffer.Dispose();
                    cpuImage.Dispose();
                    yield break;
                }

                string jsonResponse = request.downloadHandler.text;

                if (showDebugInfo)
                    AddLog($"Receive Respond : {jsonResponse}");

                try
                {
                    DetectionResponse response = JsonUtility.FromJson<DetectionResponse>(jsonResponse);

                    if (!response.success)
                    {
                        AddLog($"[ERROR] Server Error {response.error}");
                        isProcessing = false;
                        buffer.Dispose();
                        cpuImage.Dispose();
                        yield break;
                    }

                    UpdateDetectionUI(response.detections);
                }
                catch (Exception e)
                {
                    AddLog($"[ERROR] JSON Resolve Fail: {e.Message}");
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

    /// <summary>
    /// 更新检测框和 3D 模型
    /// </summary>
    void UpdateDetectionUI(Detection[] detections)
    {
        // === 1. 检测框：每次都清除并重新绘制 ===
        foreach (var box in currentBoxes)
        {
            Destroy(box);
        }
        currentBoxes.Clear();

        // === 2. 处理无检测结果的情况 ===
        if (detections == null || detections.Length == 0)
        {
            if (showDebugInfo)
                AddLog("No fruit detected");

            // 如果之前有水果显示，现在需要隐藏
            if (!string.IsNullOrEmpty(currentFruitLabel))
            {
                // 隐藏所有模型
                if (appleModel != null)
                    appleModel.SetActive(false);
                if (bananaModel != null)
                    bananaModel.SetActive(false);

                // 隐藏营养信息
                if (nutritionDisplay != null)
                    nutritionDisplay.HideNutrition();

                // 清空当前水果标签
                currentFruitLabel = "";

                AddLog("Models hidden");
            }

            return;
        }

        // === 3. 有检测结果：找到置信度最高的 ===
        Detection bestDetection = detections[0];
        foreach (var det in detections)
        {
            if (det.confidence > bestDetection.confidence)
                bestDetection = det;
        }

        AddLog($"Detected: {bestDetection.label} ({bestDetection.confidence:F2})");

        // === 4. 检测框：总是绘制（位置可能变化）===
        DrawDetectionBox(bestDetection);

        // === 5. 模型和营养信息：只在水果类型变化时更新 ===
        if (bestDetection.label != currentFruitLabel)
        {
            AddLog($"Fruit type changed: {currentFruitLabel} -> {bestDetection.label}");

            // 显示新的模型
            ShowFruitModel(bestDetection.label);

            // 显示新的营养信息
            if (nutritionDisplay != null)
            {
                nutritionDisplay.ShowNutrition(bestDetection.label);
            }

            // 更新当前水果标签
            currentFruitLabel = bestDetection.label;
        }
        else
        {
            // 水果类型没变，只需要更新检测框位置
            // 模型和营养信息保持不变
            if (showDebugInfo)
                AddLog($"Same fruit, keeping model state");
        }
    }
    /// <summary>
    /// 绘制检测框（简化版本 - 使用归一化坐标）
    /// </summary>
    void DrawDetectionBox(Detection detection)
    {
        GameObject boxObj = Instantiate(detectionBoxPrefab, detectionBoxContainer);
        RectTransform boxRect = boxObj.GetComponent<RectTransform>();

        if (boxRect == null)
        {
            AddLog("[ERROR] DetectionBoxPrefab missing RectTransform!");
            Destroy(boxObj);
            return;
        }

        // 获取屏幕尺寸
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        // 服务端返回归一化坐标 [x1, y1, x2, y2]，范围 0-1
        // 坐标系：左上角原点，X 向右，Y 向下
        float normX1 = detection.box[0];
        float normY1 = detection.box[1];
        float normX2 = detection.box[2];
        float normY2 = detection.box[3];

        if (showDebugInfo)
            AddLog($"Normalized box: [{normX1:F3}, {normY1:F3}, {normX2:F3}, {normY2:F3}]");

        // 转换为屏幕像素坐标
        float x1 = normX1 * screenWidth;
        float y1 = normY1 * screenHeight;
        float x2 = normX2 * screenWidth;
        float y2 = normY2 * screenHeight;

        // 计算中心点和尺寸
        float centerX = (x1 + x2) / 2f;
        float centerY_ImageSpace = (y1 + y2) / 2f;  // 图像坐标系（左上角原点）
        float width = x2 - x1;
        float height = y2 - y1;

        // 转换到 Unity UI 坐标系（左下角原点，Y 向上）
        float centerY_UISpace = screenHeight - centerY_ImageSpace;

        if (showDebugInfo)
            AddLog($"Box center (UI): ({centerX:F0}, {centerY_UISpace:F0}), size: {width:F0}x{height:F0}");

        // 设置 RectTransform
        boxRect.anchorMin = new Vector2(0, 0);
        boxRect.anchorMax = new Vector2(0, 0);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.anchoredPosition = new Vector2(centerX, centerY_UISpace);
        boxRect.sizeDelta = new Vector2(width, height);

        currentBoxes.Add(boxObj);
    }

    /// <summary>
    /// 显示对应的水果模型
    /// </summary>
    void ShowFruitModel(string fruitId)
    {
        // 先隐藏所有模型
        if (appleModel != null)
            appleModel.SetActive(false);
        if (bananaModel != null)
            bananaModel.SetActive(false);
        if (orangeModel != null) orangeModel.SetActive(false);  // 新增

        // 显示对应的模型
        if (fruitId == "apple" && appleModel != null)
        {
            appleModel.SetActive(true);
            AddLog("Showing Apple model");
        }
        else if (fruitId == "banana" && bananaModel != null)
        {
            bananaModel.SetActive(true);
            AddLog("Showing Banana model");
        }
        else if (fruitId == "orange" && orangeModel != null)  // 新增
        {
            orangeModel.SetActive(true);
            AddLog("Showing Orange model");

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
    public float[] box;  // 改为 float[]，接收归一化坐标 (0-1 范围)
}
