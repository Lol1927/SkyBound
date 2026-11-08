using UnityEngine;

public class VegetationCatalog : ScriptableObject
{
    public GameObject broadleafA;
    public GameObject broadleafB;
    public GameObject pineA;
    public GameObject pineB;
    public GameObject snowPineA;
    public GameObject snowPineB;
    public GameObject deadA;
    public GameObject deadB;
    public GameObject rockA;
    public GameObject rockB;
    public GameObject rockC;
    public GameObject rockD;

    public GameObject Find(string id)
    {
        switch (id)
        {
            case "broadleafA": return broadleafA;
            case "broadleafB": return broadleafB;
            case "pineA": return pineA;
            case "pineB": return pineB;
            case "snowPineA": return snowPineA;
            case "snowPineB": return snowPineB;
            case "deadA": return deadA;
            case "deadB": return deadB;
            case "rockA": return rockA;
            case "rockB": return rockB;
            case "rockC": return rockC;
            case "rockD": return rockD;
            default: return null;
        }
    }
}
