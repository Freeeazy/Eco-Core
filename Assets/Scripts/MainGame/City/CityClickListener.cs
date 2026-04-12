using UnityEngine;

public class CityClickOpener : MonoBehaviour
{
    [SerializeField] private CubeSphereCellPicker picker;
    [SerializeField] private CityManager cityManager;

    private void Awake()
    {
        if (!picker) picker = FindFirstObjectByType<CubeSphereCellPicker>();
        if (!cityManager) cityManager = FindFirstObjectByType<CityManager>();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (picker == null || cityManager == null)
                return;

            int cellIndex = picker.SelectedCellIndex;

            if (cellIndex >= 0)
            {
                cityManager.TryOpenCityUI(cellIndex);
            }
        }
    }
}