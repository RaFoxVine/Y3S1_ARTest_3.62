using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;  // 如果用方案 A

/// <summary>
/// 水果配置管理器 - 运行时加载模型和配置数据
/// </summary>
public class FruitConfigManager : MonoBehaviour
{
    public static FruitConfigManager Instance { get; private set; }

    [Header("Model Parent")]
    [SerializeField] private Transform modelParent;  // FruitModelAnchor

    private Dictionary<string, FruitEntry> fruitConfigs = new Dictionary<string, FruitEntry>();
    private Dictionary<string, GameObject> loadedModels = new Dictionary<string, GameObject>();

    void Awake()
    {
        // 单例模式
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        LoadConfig();
    }

    /// <summary>
    /// 从 Resources 加载配置文件
    /// </summary>
    void LoadConfig()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("Config/FruitConfig");
        if (jsonFile == null)
        {
            Debug.LogError("[FruitConfigManager] Config/FruitConfig.json not found in Resources!");
            return;
        }

        try
        {
            // 使用 Newtonsoft.Json 解析
            var root = JsonConvert.DeserializeObject<FruitConfigRoot>(jsonFile.text);
            fruitConfigs = root.fruits;

            Debug.Log($"[FruitConfigManager] Loaded {fruitConfigs.Count} fruit configs");

            // 打印所有已加载的水果
            foreach (var kvp in fruitConfigs)
            {
                Debug.Log($"  - {kvp.Key}: {kvp.Value.displayName}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FruitConfigManager] Failed to parse config: {e.Message}");
        }
    }

    /// <summary>
    /// 检查是否有指定水果的配置
    /// </summary>
    public bool HasFruit(string fruitId)
    {
        return fruitConfigs.ContainsKey(fruitId);
    }

    /// <summary>
    /// 获取水果配置
    /// </summary>
    public FruitEntry GetFruitEntry(string fruitId)
    {
        return fruitConfigs.TryGetValue(fruitId, out var entry) ? entry : null;
    }

    /// <summary>
    /// 获取或加载水果模型
    /// </summary>
    public GameObject GetOrLoadModel(string fruitId)
    {
        // 已加载过，直接返回
        if (loadedModels.TryGetValue(fruitId, out var existing))
        {
            return existing;
        }

        // 获取配置
        var entry = GetFruitEntry(fruitId);
        if (entry == null)
        {
            Debug.LogError($"[FruitConfigManager] Unknown fruit: {fruitId}");
            return null;
        }

        // 从 Resources 加载 Prefab
        GameObject prefab = Resources.Load<GameObject>(entry.modelPath);
        if (prefab == null)
        {
            Debug.LogError($"[FruitConfigManager] Model not found at: Resources/{entry.modelPath}");
            return null;
        }

        // 实例化到 modelParent 下
        GameObject instance = Instantiate(prefab, modelParent);
        instance.name = fruitId + "_Model";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.SetActive(false);

        // 确保有交互脚本
        if (instance.GetComponent<FruitModelInteraction>() == null)
        {
            instance.AddComponent<FruitModelInteraction>();
            Debug.Log($"[FruitConfigManager] Added FruitModelInteraction to {fruitId}");
        }

        loadedModels[fruitId] = instance;
        Debug.Log($"[FruitConfigManager] Loaded model: {fruitId} from {entry.modelPath}");

        return instance;
    }

    /// <summary>
    /// 隐藏所有模型
    /// </summary>
    public void HideAllModels()
    {
        foreach (var kvp in loadedModels)
        {
            if (kvp.Value != null)
                kvp.Value.SetActive(false);
        }
    }
}