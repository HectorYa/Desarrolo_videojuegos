using UnityEngine;
using UnityEngine.Events;

namespace MageHero
{
    public enum WaterPolicy
    {
        Block,    // collider sólido: el río es un muro, no se puede entrar
        Penalty,  // trigger: quita vida y devuelve al jugador a la última posición segura
        Defeat    // trigger: derrota inmediata
    }

    /// Volumen del cauce del río. La detección la hace WaterSensor (en el jugador),
    /// que es independiente de las reglas de eventos de trigger de Unity.
    [RequireComponent(typeof(Collider))]
    public class WaterZone : MonoBehaviour
    {
        public WaterPolicy policy = WaterPolicy.Penalty;
        [Min(0)] public int damage = 1;
        public UnityEvent<GameObject> onPlayerEntered;

        void Reset() => Apply();
        void OnValidate() => Apply();

        void Apply()
        {
            foreach (var c in GetComponents<Collider>()) c.isTrigger = policy != WaterPolicy.Block;
        }
    }
}
