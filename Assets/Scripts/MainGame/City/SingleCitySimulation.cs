using UnityEngine;

public class SingleCitySimulation : MonoBehaviour
{
    [Header("Time")]
    [SerializeField] private SunRotate sunRotate;
    [SerializeField] private float tickHours = 4f;

    [Header("Resources")]
    [SerializeField] private float food = 10f;

    [Header("Population")]
    [SerializeField] private int kids = 0;
    [SerializeField] private int adults = 10;
    [SerializeField] private int elders = 0;
    [SerializeField] private int dead = 0;

    [Header("Jobs")]
    [SerializeField] private int hunters = 0;
    [SerializeField] private int gatherers = 0;
    [SerializeField] private int caretakers = 0;

    [Header("Food Rates (per day)")]
    [SerializeField] private float hunterFoodPerDay = 3f;
    [SerializeField] private float gathererFoodPerDay = 1.5f;
    [SerializeField] private float adultFoodPerDay = 1f;
    [SerializeField] private float elderFoodPerDay = 1f;
    [SerializeField] private float kidFoodPerDay = 0.75f;

    [Header("Birth / Growth")]
    [SerializeField] private float birthsPerAdultPerDay = 0.01f;
    [SerializeField] private float daysToGrowKid = 60f;
    [SerializeField] private float caretakerGrowthBonus = 0.10f;
    [SerializeField] private float maxCaretakerGrowthBonus = 0.50f;

    [Header("Aging")]
    [SerializeField] private float adultYearsBeforeElder = 8f;
    [SerializeField] private float elderYearsBeforeDeath = 3f;

    [Header("Risk / Starvation")]
    [SerializeField] private float hunterDeathChancePerTick = 0.01f;
    [SerializeField] private float starvationDeathProgressPerMissingFood = 0.25f;

    [Header("Debug")]
    [SerializeField] private float accumulatedGameHours = 0f;
    [SerializeField] private float birthProgress = 0f;
    [SerializeField] private float kidGrowthProgress = 0f;
    [SerializeField] private float adultAgingProgress = 0f;
    [SerializeField] private float elderDeathProgress = 0f;
    [SerializeField] private float starvationDeathProgress = 0f;

    private float lastTimeOfDay;

    private bool isActive = false;
    public float Food => food;

    public int Kids => kids;
    public int Adults => adults;
    public int Elders => elders;
    public int Dead => dead;

    public int Hunters => hunters;
    public int Gatherers => gatherers;
    public int Caretakers => caretakers;
    public int Unemployed => Mathf.Max(0, adults - hunters - gatherers - caretakers);

    public int LivingPopulation => kids + adults + elders;
    public int TotalPopulation => kids + adults + elders + dead;

    private void Start()
    {
        if (!sunRotate)
            sunRotate = FindFirstObjectByType<SunRotate>();

        if (sunRotate != null)
            lastTimeOfDay = sunRotate.GetTimeOfDay();
    }

    private void OnEnable()
    {
        CityManager.OnFirstCityPlaced += ActivateSimulation;
    }

    private void OnDisable()
    {
        CityManager.OnFirstCityPlaced -= ActivateSimulation;
    }

    private void ActivateSimulation()
    {
        isActive = true;
        Debug.Log("City simulation started!");
    }

    private void Update()
    {
        if (!isActive)
            return;

        if (sunRotate == null)
            return;

        float currentTimeOfDay = sunRotate.GetTimeOfDay();
        float normalizedDelta = currentTimeOfDay - lastTimeOfDay;

        // Handle wraparound from 0.99 -> 0.01
        if (normalizedDelta < 0f)
            normalizedDelta += 1f;

        lastTimeOfDay = currentTimeOfDay;

        float gameHoursPassed = normalizedDelta * 24f;
        accumulatedGameHours += gameHoursPassed;

        while (accumulatedGameHours >= tickHours)
        {
            SimulateTick(tickHours);
            accumulatedGameHours -= tickHours;
        }
    }

    private void SimulateTick(float hours)
    {
        float dayFraction = hours / 24f;

        // 1. Food production
        float producedFood =
            (hunters * hunterFoodPerDay * dayFraction) +
            (gatherers * gathererFoodPerDay * dayFraction);

        food += producedFood;

        // 2. Food consumption
        float consumedFood =
            (adults * adultFoodPerDay * dayFraction) +
            (elders * elderFoodPerDay * dayFraction) +
            (kids * kidFoodPerDay * dayFraction);

        food -= consumedFood;

        // 3. Hunter death chance
        for (int i = 0; i < hunters; i++)
        {
            if (Random.value < hunterDeathChancePerTick)
            {
                hunters--;
                adults--;
                dead++;
                i--;

                if (adults < 0) adults = 0;
                ClampJobsToAdults();
            }
        }

        // 4. Births
        if (food > 0f && adults > 0)
        {
            float population = LivingPopulation;

            // avoid divide by zero + gives nicer early-game behavior
            float foodRatio = food / (population + 1f);

            // clamp so it doesn't go insane
            float foodBonus = Mathf.Clamp(foodRatio, 0f, 2f);

            float finalBirthRate = birthsPerAdultPerDay * (1f + foodBonus);

            birthProgress += adults * finalBirthRate * dayFraction;

            int newKids = Mathf.FloorToInt(birthProgress);

            if (newKids > 0)
            {
                kids += newKids;
                birthProgress -= newKids;
            }
        }

        // 5. Kid growth into adults
        if (kids > 0)
        {
            float caretakerBonus = Mathf.Min(caretakers * caretakerGrowthBonus, maxCaretakerGrowthBonus);
            float growthMultiplier = 1f + caretakerBonus;

            float kidsToAdultsPerDayPerKid = (1f / daysToGrowKid) * growthMultiplier;
            kidGrowthProgress += kids * kidsToAdultsPerDayPerKid * dayFraction;

            int grownKids = Mathf.Min(kids, Mathf.FloorToInt(kidGrowthProgress));
            if (grownKids > 0)
            {
                kids -= grownKids;
                adults += grownKids;
                kidGrowthProgress -= grownKids;
            }
        }

        // 6. Adults age into elders
        if (adults > 0)
        {
            float adultDaysLifetime = adultYearsBeforeElder * 60f;
            float adultsToEldersPerDayPerAdult = 1f / adultDaysLifetime;

            adultAgingProgress += adults * adultsToEldersPerDayPerAdult * dayFraction;

            int newElders = Mathf.Min(adults, Mathf.FloorToInt(adultAgingProgress));
            if (newElders > 0)
            {
                adults -= newElders;
                elders += newElders;
                adultAgingProgress -= newElders;
                ClampJobsToAdults();
            }
        }

        // 7. Elders die naturally
        if (elders > 0)
        {
            float elderDaysLifetime = elderYearsBeforeDeath * 60f;
            float eldersToDeadPerDayPerElder = 1f / elderDaysLifetime;

            elderDeathProgress += elders * eldersToDeadPerDayPerElder * dayFraction;

            int elderDeaths = Mathf.Min(elders, Mathf.FloorToInt(elderDeathProgress));
            if (elderDeaths > 0)
            {
                elders -= elderDeaths;
                dead += elderDeaths;
                elderDeathProgress -= elderDeaths;
            }
        }

        // 8. Starvation
        if (food < 0f)
        {
            float missingFood = -food;
            food = 0f;

            starvationDeathProgress += missingFood * starvationDeathProgressPerMissingFood;

            while (starvationDeathProgress >= 1f)
            {
                if (!KillOneForStarvation())
                    break;

                starvationDeathProgress -= 1f;
            }
        }
    }

    private bool KillOneForStarvation()
    {
        if (elders > 0)
        {
            elders--;
            dead++;
            return true;
        }

        if (adults > 0)
        {
            adults--;
            dead++;
            ClampJobsToAdults();
            return true;
        }

        if (kids > 0)
        {
            kids--;
            dead++;
            return true;
        }

        return false;
    }

    private void ClampJobsToAdults()
    {
        while (hunters + gatherers + caretakers > adults)
        {
            if (caretakers > 0)
            {
                caretakers--;
            }
            else if (gatherers > 0)
            {
                gatherers--;
            }
            else if (hunters > 0)
            {
                hunters--;
            }
            else
            {
                break;
            }
        }
    }

    public bool AddHunter()
    {
        if (Unemployed <= 0)
            return false;

        hunters++;
        return true;
    }

    public bool RemoveHunter()
    {
        if (hunters <= 0)
            return false;

        hunters--;
        return true;
    }

    public bool AddGatherer()
    {
        if (Unemployed <= 0)
            return false;

        gatherers++;
        return true;
    }

    public bool RemoveGatherer()
    {
        if (gatherers <= 0)
            return false;

        gatherers--;
        return true;
    }

    public bool AddCaretaker()
    {
        if (Unemployed <= 0)
            return false;

        caretakers++;
        return true;
    }

    public bool RemoveCaretaker()
    {
        if (caretakers <= 0)
            return false;

        caretakers--;
        return true;
    }
}