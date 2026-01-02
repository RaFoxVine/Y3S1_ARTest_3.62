using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class NutritionDisplayManager : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private GameObject nutritionPanel;
    [SerializeField] private TextMeshProUGUI fruitNameText;
    [SerializeField] private TextMeshProUGUI nutritionInfoText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    void Start()
    {
        if (nutritionPanel != null)
            nutritionPanel.SetActive(false);
    }

    /// <summary>
    /// 显示指定水果的营养信息（从配置管理器获取）
    /// </summary>
    public void ShowNutrition(string fruitId)
    {
        if (string.IsNullOrEmpty(fruitId))
        {
            HideNutrition();
            return;
        }

        // 从配置管理器获取数据
        var entry = FruitConfigManager.Instance.GetFruitEntry(fruitId);
        if (entry == null)
        {
            Debug.LogWarning($"[NutritionDisplay] No config for: {fruitId}");
            HideNutrition();
            return;
        }

        // 更新 UI 文本
        if (fruitNameText != null)
            fruitNameText.text = entry.displayName;

        if (nutritionInfoText != null)
        {
            string nutritionText = BuildNutritionText(entry.nutrition);
            nutritionInfoText.text = nutritionText;
        }

        if (descriptionText != null)
            descriptionText.text = entry.nutrition.description;

        // 显示面板
        if (nutritionPanel != null)
            nutritionPanel.SetActive(true);

        Debug.Log($"[NutritionDisplay] Showing nutrition for {entry.displayName}");
    }

    /// <summary>
    /// 构建营养信息文本
    /// </summary>
    string BuildNutritionText(FruitNutrition n)
    {
        List<string> parts = new List<string>();

        if (n.calories > 0)
            parts.Add($"Calories: {n.calories} kcal");

        if (n.carbs > 0)
            parts.Add($"Carbs: {n.carbs}g");

        if (n.fiber > 0)
            parts.Add($"Fiber: {n.fiber}g");

        if (n.sugar > 0)
            parts.Add($"Sugar: {n.sugar}g");

        if (n.vitamin_c > 0)
            parts.Add($"Vitamin C: {n.vitamin_c}%");

        if (n.potassium > 0)
            parts.Add($"Potassium: {n.potassium}mg");

        return string.Join("\n", parts);
    }

    public void HideNutrition()
    {
        if (nutritionPanel != null)
            nutritionPanel.SetActive(false);
    }

    //string BuildNutritionText(FruitData data)
    //{
    //    List<string> parts = new List<string>();

    //    if (data.calories > 0)
    //        parts.Add($"Calories: {data.calories} kcal");

    //    if (data.carbs > 0)
    //        parts.Add($"Carbs: {data.carbs}g");

    //    if (data.fiber > 0)
    //        parts.Add($"Fiber: {data.fiber}g");

    //    if (data.sugar > 0)
    //        parts.Add($"Sugar: {data.sugar}g");

    //    if (data.vitamin_c > 0)
    //        parts.Add($"Vitamin C: {data.vitamin_c}%");

    //    if (data.potassium > 0)
    //        parts.Add($"Potassium: {data.potassium}mg");

    //    return string.Join("\n", parts);  // 用换行连接
    //}
}