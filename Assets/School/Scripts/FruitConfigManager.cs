using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// 水果配置管理器 - 只管理配置数据，不管理模型加载
/// </summary>
public class FruitConfigManager : MonoBehaviour
{
    public static FruitConfigManager Instance { get; private set; }

    [Header("Config File")]
    [SerializeField] private TextAsset fruitConfigFile;  // 直接在面板引用

    private Dictionary<string, FruitEntry> fruitConfigs = new Dictionary<string, FruitEntry>();

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
    /// 加载配置文件
    /// </summary>
    void LoadConfig()
    {
        if (fruitConfigFile == null)
        {
            Debug.LogError("[FruitConfigManager] Fruit config file not assigned!");
            return;
        }

        try
        {
            // 解析 JSON
            var root = JsonConvert.DeserializeObject<FruitConfigRoot>(fruitConfigFile.text);
            fruitConfigs = root.fruits;

            Debug.Log($"[FruitConfigManager] Loaded {fruitConfigs.Count} fruit configs");

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
}