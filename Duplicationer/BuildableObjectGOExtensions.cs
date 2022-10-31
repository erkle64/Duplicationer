using Knife.PostProcessing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Duplicationer
{
    public static class BuildableObjectGOExtensions
    {
        public static void setBuildableTint(this BuildableObjectGO self, Color color, Color borderColor)
        {
            foreach(var renderer in self.gameObject.GetComponentsInChildren<Renderer>())
            {
                if (renderer.material != null) renderer.material.color = color;
            }

            foreach(var outline in self.gameObject.GetComponentsInChildren<OutlineRegister>())
            {
                outline.SetTintColor(borderColor);
            }
        }
    }
}
