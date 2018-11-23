using UnityEngine;
using System.Collections;

public class LayerManager : MonoBehaviour {

    //easy access to layermanager
    public static LayerManager instance
    {
        get
        {
            if(!_instance)
            {
                _instance = FindObjectOfType<LayerManager>();
            }

            return _instance;
        }
    }

    private static LayerManager _instance;

    public LayerMask groundLayer;
    public LayerMask ladderLayer;
    public LayerMask portalLayer;
    public LayerMask onewayLayer;

    public void Awake()
    {
        if (!instance)
        {
            _instance = this;
        }
    }
}
