using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace FZTools
{
    [CreateAssetMenu(fileName = "FZDressUpBodyConstants", menuName = "FZTools/FZDressUpBodyConstants", order = 0)]
    public class FZDressUpBodyConstants : ScriptableObject
    {
        [SerializeField]
        public List<string> DressUpBodyNames = new List<string>()
        {
            "body", "underware", "bra", "shorts", "socks",
            "tights", "leggins", "hair", "ear", "horn",
            "face", "spats", "tail", "garter", "underwear", "Body"
        };
        [SerializeField]
        public List<string> ShrinkShapeNames = new List<string>() {
            "shrink", "シュリンク", "ｼｭﾘﾝｸ"
        };
    }
}