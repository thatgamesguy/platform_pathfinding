using UnityEngine;
using System.Collections;

/// <summary>
/// Provides access to LayerMasks (currently only the ground layer).
/// </summary>
public class LayerManager : MonoBehaviour
{
    /// <summary>
    /// Singleton.
    /// </summary>
    public static LayerManager instance
    {
        get
        {
            if(!_instance)
            {
                _instance = GameObject.FindObjectOfType<LayerManager>();
            }

            return _instance;
        }
    }

    private static LayerManager _instance;

    public LayerMask groundLayer;

    public void Awake()
    {
        if (!_instance)
        {
            _instance = this;
        }
    }
}
