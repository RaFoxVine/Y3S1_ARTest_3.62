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

    [Header("Data Config")]
    [SerializeField] private TextAsset nutritionJsonFile;

    private FruitNutritionDatabase nutritionDatabase;
    private Dictionary<string, FruitData> fruitDataDict = new Dictionary<string, FruitData>();

    void Start()
    {
        if (nutritionPanel != null)
            nutritionPanel.SetActive(false);

        LoadNutritionData();
    }

    void LoadNutritionData()
    {
        if (nutritionJsonFile == null)
        {
            Debug.LogError("[NutritionDisplay] JSON file not assigned!");
            return;
        }

        try
        {
            nutritionDatabase = JsonUtility.FromJson<FruitNutritionDatabase>(nutritionJsonFile.text);

            fruitDataDict.Clear();
            foreach (var fruit in nutritionDatabase.fruits)
            {
                fruitDataDict[fruit.name] = fruit;
            }

            Debug.Log($"[NutritionDisplay] Loaded {fruitDataDict.Count} fruits");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NutritionDisplay] JSON parse failed: {e.Message}");
        }
    }

    public void ShowNutrition(string fruitName)
    {
        if (string.IsNullOrEmpty(fruitName))
        {
            HideNutrition();
            return;
        }

        if (!fruitDataDict.ContainsKey(fruitName))
        {
            Debug.LogWarning($"[NutritionDisplay] Fruit data not found: {fruitName}");
            HideNutrition();
            return;
        }

        FruitData data = fruitDataDict[fruitName];

        if (fruitNameText != null)
            fruitNameText.text = data.displayName;

        if (nutritionInfoText != null)
        {
            string nutritionText = BuildNutritionText(data);
            nutritionInfoText.text = nutritionText;
        }

        if (descriptionText != null)
            descriptionText.text = data.description;

        if (nutritionPanel != null)
            nutritionPanel.SetActive(true);

        Debug.Log($"[NutritionDisplay] Showing nutrition for {data.displayName}");
    }

    public void HideNutrition()
    {
        if (nutritionPanel != null)
            nutritionPanel.SetActive(false);
    }

    string BuildNutritionText(FruitData data)
    {
        List<string> parts = new List<string>();

        if (data.calories > 0)
            parts.Add($"Calories: {data.calories} kcal");

        if (data.carbs > 0)
            parts.Add($"Carbs: {data.carbs}g");

        if (data.fiber > 0)
            parts.Add($"Fiber: {data.fiber}g");

        if (data.sugar > 0)
            parts.Add($"Sugar: {data.sugar}g");

        if (data.vitamin_c > 0)
            parts.Add($"Vitamin C: {data.vitamin_c}%");

        if (data.potassium > 0)
            parts.Add($"Potassium: {data.potassium}mg");

        return string.Join("\n", parts);  // 用换行连接
    }
}