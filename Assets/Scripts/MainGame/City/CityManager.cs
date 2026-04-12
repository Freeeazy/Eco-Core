using System;
using System.Collections.Generic;
using UnityEngine;

public class CityManager : MonoBehaviour
{
    [Header("City UI")]
    [SerializeField] private GameObject cityPanel;

    // simple temp storage: cell index -> city name
    private Dictionary<int, string> cities = new Dictionary<int, string>();

    public static event Action OnFirstCityPlaced;

    public bool HasCityAtCell(int cellIndex)
    {
        return cities.ContainsKey(cellIndex);
    }

    public void RegisterCity(int cellIndex, string cityName = "New City")
    {
        if (!cities.ContainsKey(cellIndex))
        {
            bool wasEmpty = cities.Count == 0;

            cities.Add(cellIndex, cityName);

            if (wasEmpty)
            {
                Debug.Log("First city placed!");
                OnFirstCityPlaced?.Invoke();
            }
        }
    }

    public string GetCityName(int cellIndex)
    {
        if (cities.TryGetValue(cellIndex, out string cityName))
            return cityName;

        return string.Empty;
    }

    public void TryOpenCityUI(int cellIndex)
    {
        if (HasCityAtCell(cellIndex))
        {
            if (cityPanel != null)
                cityPanel.SetActive(true);

            Debug.Log("Opened city UI for: " + GetCityName(cellIndex));
        }
    }

    public void CloseCityUI()
    {
        if (cityPanel != null)
            cityPanel.SetActive(false);
    }
}