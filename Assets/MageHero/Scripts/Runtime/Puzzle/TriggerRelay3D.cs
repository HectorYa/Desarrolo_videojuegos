using System;
using UnityEngine;

namespace MageHero
{
    /// Reenvía los eventos de un Trigger Collider (hijo) a quien lo escuche, filtrando por el tag Player.
    /// Así un mismo manager puede tener varias zonas (colapso, reparación...).
    [RequireComponent(typeof(Collider))]
    public class TriggerRelay3D : MonoBehaviour
    {
        public string requiredTag = "Player";
        public event Action<Collider> Entered, Exited;
        public bool PlayerInside { get; private set; }

        void Reset() { GetComponent<Collider>().isTrigger = true; }
        void Awake() { GetComponent<Collider>().isTrigger = true; }

        void OnTriggerEnter(Collider other)
        {
            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;
            PlayerInside = true;
            Entered?.Invoke(other);
        }

        void OnTriggerExit(Collider other)
        {
            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;
            PlayerInside = false;
            Exited?.Invoke(other);
        }

        void OnDisable() => PlayerInside = false;
    }
}
