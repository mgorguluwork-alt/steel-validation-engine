namespace SteelValidation.Abstractions
{
    public class ClashCheckItem
    {
        // Arka planda kullanilir.
        // UI'da Object ID gostermeyecegiz.
        public int Object1Id { get; set; }
        public int Object2Id { get; set; }

        // Operatorun gorecegi bilgiler.
        public string ClashType { get; set; }

        public string Object1 { get; set; }
        public string Object2 { get; set; }

        public string Description { get; set; }

        public double Overlap { get; set; }

        public string Severity { get; set; }

        public string Status { get; set; }
    }
}