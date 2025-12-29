using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 水果营养数据的序列化类
/// </summary>
[Serializable]
public class FruitNutritionDatabase
{
    public List<FruitData> fruits;
}

[Serializable]
public class FruitData
{
    public string name;           // 水果 ID（apple, banana）
    public string displayName;    // 显示名称（苹果 Apple）
    public int calories;          // 热量 (kcal)
    public float carbs;           // 碳水化合物 (g)
    public float fiber;           // 膳食纤维 (g)
    public float sugar;           // 糖分 (g)
    public int vitamin_c;         // 维生素C (%)
    public int potassium;         // 钾 (mg)
    public string description;    // 描述
}