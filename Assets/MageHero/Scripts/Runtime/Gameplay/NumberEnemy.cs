using UnityEngine;

namespace MageHero
{
    /// Enemigo numérico decorativo: rebota, "respira" y gira hacia el jugador si está cerca.
    /// Guarda su ejercicio para conectarlo después con el sistema de problemas.
    public class NumberEnemy : MonoBehaviour
    {
        public string symbol = "+";
        public string exercise = "1 + 1";
        public int answer = 2;
        float baseY, phase;
        Transform player;

        void Start()
        {
            baseY = transform.position.y;
            phase = Random.value * 6f;
            var p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }

        void Update()
        {
            float t = Time.time, w = Mathf.Sin(t * 3.2f + phase) * 0.03f;
            Vector3 pos = transform.position; pos.y = baseY + Mathf.Abs(Mathf.Sin(t * 1.6f + phase)) * 0.12f; transform.position = pos;
            transform.localScale = new Vector3(1f + w, 1f - w, 1f + w);
            if (player == null) return;
            Vector3 d = player.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 196f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1.8f * Time.deltaTime);
        }
    }
}
