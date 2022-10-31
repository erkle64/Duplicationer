using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Duplicationer
{
    public class LazyMaterial
    {
        private Material material = null;
        private BuildMaterialDelegate buildMaterial;

        public delegate Material BuildMaterialDelegate();

        public LazyMaterial(BuildMaterialDelegate buildMaterial)
        {
            this.buildMaterial = buildMaterial;
        }

        public Material Material => material == null ? material = buildMaterial.Invoke() : material;
    }
}
