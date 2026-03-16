using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // TextMeshPro pro hezčí texty

namespace WarehouseSim.Managers
{
    /// <summary>
    /// Tycoon-style vyskakovací notifikace nahrazující Unity Debug.Log(). 
    /// Okna sama po několika vteřinách umírají. Vhodné pro informování hráče v rohu obrazovky.
    /// </summary>
    public class NotificationManager : MonoBehaviour
    {
        public static NotificationManager Instance { get; private set; }

        [Header("UI Napojení")]
        [Tooltip("Zde přetáhni svůj UI Panel s VerticalLayoutGroup, který bude notifikace řadit pod sebe.")]
        public Transform notificationContainer;
        
        [Tooltip("Zde přetáhni Prefab obyčejného TextMeshPro Textu (nebo celého oknečka s textem)")]
        public GameObject notificationPrefab;
        
        [Header("Nastavení (Fáze 22)")]
        public float defaultDuration = 4f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void ShowMessage(string message, Color color)
        {
            if (notificationPrefab == null || notificationContainer == null) return;
            
            GameObject notif = Instantiate(notificationPrefab, notificationContainer);
            
            // Cvičný pokus chytit samotný text
            TextMeshProUGUI txt = notif.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = message;
                txt.color = color;
            }

            // Za daný čas (neovlivněný rychlostí simulace TimeScale) notifikaci zahubíme!
            StartCoroutine(DestroyNotificationRoutine(notif));
        }

        private IEnumerator DestroyNotificationRoutine(GameObject obj)
        {
            // Unscaled time zajistí, že i když je hra přes slider pauznutá, notifikace zmizí a nebude viset do smrti na obrazovce
            yield return new WaitForSecondsRealtime(defaultDuration);
            if (obj != null) Destroy(obj);
        }

        // ===================================
        // WRAPPERY PRO RYCHLÉ VOLÁNÍ (Šetří čas)
        // ===================================
        public static void LogInfo(string msg) 
        { 
            if (Instance != null) Instance.ShowMessage(msg, Color.white); 
            Debug.Log(msg); // Záložní paralelní tisk do unity konzole
        }
        
        public static void LogWarning(string msg) 
        { 
            if (Instance != null) Instance.ShowMessage("⚠ " + msg, Color.yellow); 
            Debug.LogWarning(msg);
        }
        
        public static void LogError(string msg) 
        { 
            if (Instance != null) Instance.ShowMessage("❌ " + msg, Color.red); 
            Debug.LogError(msg);
        }
        
        public static void LogSuccess(string msg) 
        { 
            if (Instance != null) Instance.ShowMessage("✓ " + msg, Color.green); 
            Debug.Log(msg);
        }
    }
}
