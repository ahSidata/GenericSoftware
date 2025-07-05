namespace EnergyAutomate.Definitions
{
    public static class GrowattElements
    {
        #region Fields

        public static readonly GrowattElement Adjustment1 = new GrowattElement
        {
            Id = new Guid("{E70F66C1-784F-4F3A-B7C5-373EC2D7962E}"),
            ElementType = GrowattElement.ElementTypes.Adjustment,
            Name = "Adjustment 1",
            IsActive = true
        };

        public static readonly GrowattElement Adjustment2 = new GrowattElement
        {
            Id = new Guid("{77441D16-75E8-44B3-B371-81513EB99AC1}"),
            ElementType = GrowattElement.ElementTypes.Adjustment,
            Name = "Adjustment 2",
            IsActive = false
        };

        public static readonly GrowattElement Adjustment3 = new GrowattElement
        {
            Id = new Guid("{35BF934B-8597-4D1B-ACAF-9D1A631201FA}"),
            ElementType = GrowattElement.ElementTypes.Adjustment,
            Name = "Adjustment 3",
            IsActive = false
        };

        public static readonly GrowattElement Calculation1 = new GrowattElement
        {
            Id = new Guid("{8818719E-2DF1-4415-B6CE-4ECD0F56B07E}"),
            ElementType = GrowattElement.ElementTypes.Calculation,
            Name = "Calculation 1",
            IsActive = true
        };

        public static readonly GrowattElement Calculation2 = new GrowattElement
        {
            Id = new Guid("{B0557C71-EC01-4D17-AF74-DEA54D7B437F}"),
            ElementType = GrowattElement.ElementTypes.Calculation,
            Name = "Calculation 2",
            IsActive = false
        };

        public static readonly GrowattElement Distribution1 = new GrowattElement
        {
            Id = new Guid("{FB27517E-BE1A-48A6-8037-1534049743EB}"),
            ElementType = GrowattElement.ElementTypes.Distribution,
            Name = "Distribution1",
            IsActive = false
        };

        public static readonly GrowattElement Distribution2 = new GrowattElement
        {
            Id = new Guid("{046BF701-320D-4B05-BD92-D27B7B386BDB}"),
            ElementType = GrowattElement.ElementTypes.Distribution,
            Name = "Distribution2",
            IsActive = true
        };

        #endregion Fields

        #region Public Methods

        public static IEnumerable<GrowattElement> GrowattDefaultElements()
        {
            return new List<GrowattElement> { Adjustment1, Adjustment2, Adjustment3, Calculation1, Calculation2, Distribution1, Distribution2 };
        }

        #endregion Public Methods
    }
}
