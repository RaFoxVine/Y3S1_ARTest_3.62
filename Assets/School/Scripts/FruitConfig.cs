using System;
using System.Collections.Generic;

/// <summary>
/// 水果营养信息
/// </summary>
[Serializable]
public class FruitNutrition
{
    public float calories;
    public float carbs;
    public float fiber;
    public float sugar;
    public string description;

    // 可选营养素（不同水果可能有）
    public float vitamin_c;
    public float potassium;
}

/// <summary>
/// 单个水果的配置
/// </summary>
[Serializable]
public class FruitEntry
{
    public string displayName;
    public string modelPath;
    public FruitNutrition nutrition;
}

/// <summary>
/// JSON 根节点包装类
/// </summary>
[Serializable]
public class FruitConfigRoot
{
    public Dictionary<string, FruitEntry> fruits;
}