using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FreeBodySolver.Models
{
    public class Line
    {
        public int Id { get; set; }
        public int A { get; set; }
        public int B { get; set; }

        // Atanan kesitin kütüphanedeki adını tutacak yeni özellik
        public string SectionName { get; set; }
        // Çizginin profil orientasyonu (derece)
        public float Orientation { get; set; }

        public Line(int id, int a, int b, string sectionName = "", float orientation = 0f)
        {
            Id = id;
            A = a;
            B = b;
            SectionName = sectionName;
            Orientation = orientation;
        }
    }
}
