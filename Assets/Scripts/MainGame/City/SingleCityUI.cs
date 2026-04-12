using TMPro;
using UnityEngine;

public class SingleCityUI : MonoBehaviour
{
    [SerializeField] private SingleCitySimulation citySim;

    [Header("Top Stats")]
    [SerializeField] private TextMeshProUGUI totalPopText;
    [SerializeField] private TextMeshProUGUI foodText;

    [Header("Jobs")]
    [SerializeField] private TextMeshProUGUI unemployedText;
    [SerializeField] private TextMeshProUGUI huntersText;
    [SerializeField] private TextMeshProUGUI gatherersText;
    [SerializeField] private TextMeshProUGUI caretakersText;

    [Header("Population Breakdown")]
    [SerializeField] private TextMeshProUGUI kidsText;
    [SerializeField] private TextMeshProUGUI adultsText;
    [SerializeField] private TextMeshProUGUI eldersText;
    [SerializeField] private TextMeshProUGUI deadText;

    private void Awake()
    {
        if (!citySim)
            citySim = FindFirstObjectByType<SingleCitySimulation>();
    }

    private void Update()
    {
        if (citySim == null)
            return;

        if (totalPopText) totalPopText.text = $"Population: {citySim.LivingPopulation:N0}";
        if (foodText) foodText.text = $"Food: {citySim.Food:N0}";

        if (unemployedText) unemployedText.text = $"[{citySim.Unemployed}]";
        if (huntersText) huntersText.text = $"[{citySim.Hunters}]";
        if (gatherersText) gatherersText.text = $"[{citySim.Gatherers}]";
        if (caretakersText) caretakersText.text = $"[{citySim.Caretakers}]";

        if (kidsText) kidsText.text = $"Kids: {citySim.Kids}";
        if (adultsText) adultsText.text = $"Adult: {citySim.Adults}";
        if (eldersText) eldersText.text = $"Elder: {citySim.Elders}";
        if (deadText) deadText.text = $"Dead: {citySim.Dead}";
    }

    public void AddHunter() => citySim?.AddHunter();
    public void RemoveHunter() => citySim?.RemoveHunter();

    public void AddGatherer() => citySim?.AddGatherer();
    public void RemoveGatherer() => citySim?.RemoveGatherer();

    public void AddCaretaker() => citySim?.AddCaretaker();
    public void RemoveCaretaker() => citySim?.RemoveCaretaker();
}