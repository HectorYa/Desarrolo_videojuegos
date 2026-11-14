using System.Collections.Generic;
using UnityEngine;

namespace MageHero
{
    /// Genera ecuaciones aritméticas aleatorias (operaciones combinadas y número faltante)
    /// con 4 opciones (1 correcta + 3 distractores plausibles) y una guía paso a paso.
    [System.Serializable]
    public class ArithmeticEquation
    {
        public string text;          // "7 + 3 × 4 = ?"
        public int answer;
        public int[] options = new int[4];
        public int correctIndex;
        public string guide;         // pasos concretos para ESTA ecuación (sin dar el resultado final)
        public string tip;           // recordatorio corto que se muestra tras un fallo

        public const string GeneralRules =
            "REGLA DE ORO (jerarquía de operaciones)\n" +
            "1. Primero resuelve lo que está entre paréntesis ( ).\n" +
            "2. Después las multiplicaciones (×) y divisiones (÷), de izquierda a derecha.\n" +
            "3. Al final las sumas (+) y restas (−), de izquierda a derecha.\n\n" +
            "SI FALTA UN NÚMERO (?)\n" +
            "Usa la operación inversa: la multiplicación se deshace dividiendo\n" +
            "y la división se deshace multiplicando. ¡Comprueba tu resultado al final!";

        public static ArithmeticEquation Random()
        {
            var e = new ArithmeticEquation();
            var wrong = new List<int>();
            switch (UnityEngine.Random.Range(0, 6))
            {
                case 0: // a + b × c
                {
                    int a = R(2, 20), b = R(2, 9), c = R(2, 9), p = b * c;
                    e.answer = a + p;
                    e.text = $"{a} + {b} × {c} = ?";
                    e.guide = $"1) La multiplicación va antes que la suma: {b} × {c} = {p}.\n2) Ahora suma: {a} + {p} = ?";
                    e.tip = "Recuerda: primero × y ÷, después + y −.";
                    wrong.Add((a + b) * c); wrong.Add(a + b + c); wrong.Add(e.answer + b);
                    break;
                }
                case 1: // (a + b) ÷ c
                {
                    int c = R(2, 9), q = R(2, 10), s = c * q, a = R(1, s - 1), b = s - a;
                    e.answer = q;
                    e.text = $"({a} + {b}) ÷ {c} = ?";
                    e.guide = $"1) Primero el paréntesis: {a} + {b} = {s}.\n2) Ahora divide: {s} ÷ {c} = ?\n   Pista: ¿qué número multiplicado por {c} da {s}?";
                    e.tip = "Resuelve primero lo que está entre paréntesis.";
                    wrong.Add(s); wrong.Add(q + c); wrong.Add(q + 1);
                    break;
                }
                case 2: // a × b − c
                {
                    int a = R(2, 9), b = R(2, 9), p = a * b, c = R(1, p - 1);
                    e.answer = p - c;
                    e.text = $"{a} × {b} − {c} = ?";
                    e.guide = $"1) La multiplicación va primero: {a} × {b} = {p}.\n2) Ahora resta: {p} − {c} = ?";
                    e.tip = "Recuerda: primero multiplica, luego resta.";
                    if (b - c > 0) wrong.Add(a * (b - c));
                    wrong.Add(p + c); wrong.Add(e.answer - 1);
                    break;
                }
                case 3: // ? × b = p
                {
                    int b = R(2, 9), x = R(2, 12), p = b * x;
                    e.answer = x;
                    e.text = $"? × {b} = {p}";
                    e.guide = $"1) Falta un número que, multiplicado por {b}, da {p}.\n2) La multiplicación se deshace dividiendo: ? = {p} ÷ {b}.\n3) Calcula la división y comprueba: tu número × {b} debe dar {p}.";
                    e.tip = "Usa la operación inversa: divide.";
                    wrong.Add(p - b); wrong.Add(x + 1); wrong.Add(x + b);
                    break;
                }
                case 4: // p ÷ ? = q
                {
                    int x = R(2, 9), q = R(2, 10), p = x * q;
                    e.answer = x;
                    e.text = $"{p} ÷ ? = {q}";
                    e.guide = $"1) Buscamos el número entre el que se divide {p} para obtener {q}.\n2) Piensa al revés: {q} × ? = {p}, así que ? = {p} ÷ {q}.\n3) Calcula y comprueba: {p} ÷ tu número debe dar {q}.";
                    e.tip = "Piensa al revés: ¿qué número por " + q + " da " + p + "?";
                    wrong.Add(p - q); wrong.Add(x + 1); wrong.Add(q);
                    break;
                }
                default: // a ÷ b + c
                {
                    int b = R(2, 9), q = R(2, 9), a = b * q, c = R(1, 15);
                    e.answer = q + c;
                    e.text = $"{a} ÷ {b} + {c} = ?";
                    e.guide = $"1) La división va antes que la suma: {a} ÷ {b} = {q}.\n2) Ahora suma: {q} + {c} = ?";
                    e.tip = "Recuerda: primero ÷, después +.";
                    wrong.Add(a + c); wrong.Add(q * c); wrong.Add(e.answer + 1);
                    break;
                }
            }
            e.BuildOptions(wrong);
            return e;
        }

        void BuildOptions(List<int> candidates)
        {
            var set = new List<int> { answer };
            foreach (int w in candidates)
                if (w > 0 && !set.Contains(w) && set.Count < 4) set.Add(w);
            int k = 1;
            while (set.Count < 4)
            {
                int w = answer + (UnityEngine.Random.value < 0.5f ? -k : k);
                if (w > 0 && !set.Contains(w)) set.Add(w);
                k++;
            }
            for (int i = set.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (set[i], set[j]) = (set[j], set[i]); }
            options = set.ToArray();
            correctIndex = set.IndexOf(answer);
        }

        static int R(int min, int maxInclusive) => UnityEngine.Random.Range(min, maxInclusive + 1);
    }
}
