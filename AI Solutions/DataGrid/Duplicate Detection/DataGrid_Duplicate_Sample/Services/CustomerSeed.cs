namespace DataGrid_Duplicate_Sample;

public static class CustomerSeed
{
    public static List<Customer> Build()
    {
        var list = new List<Customer>
        {
            new() { Id="C001", Name="Robert Smith",  Email="robert.smith@acme.com", Phone="(415) 555-0142", Address="100 Market St",     City="San Francisco", State="CA", PostalCode="94103", Company="Acme Corp" },
            new() { Id="C002", Name="Robt Smith",    Email="robert.smith@acme.com", Phone="4155550142",      Address="100 Market Street", City="San Francisco", State="CA", PostalCode="94103", Company="Acme Corporation" },
            new() { Id="C003", Name="David Wong",    Email="d.wong@initech.com",    Phone="(512) 555-0117", Address="900 Congress Ave",  City="Austin",       State="TX", PostalCode="78701", Company="Initech LLC" },
            new() { Id="C005", Name="Bruce Wayne",   Email="bruce.wayne@wayne.com", Phone="(312) 555-0199", Address="1 Wayne Tower",     City="Chicago",      State="IL", PostalCode="60601", Company="Wayne Enterprises" },
            new() { Id="C006", Name="Selina Kyle",   Email="selina.kyle@gotham.com",Phone="(312) 555-0167", Address="2 Gotham Plaza",    City="Chicago",      State="IL", PostalCode="60602", Company="Independent" },
            new() { Id="C004", Name="Dave Wong",     Email="d.wong@initech.com",    Phone="(512) 555-1170", Address="900 Congress Ave",  City="Austin",       State="TX", PostalCode="78701", Company="Initech" },
        };

        foreach (var varName in list)
        {
            varName.Status      = "Unique";
            varName.Match       = "—";
            varName.RowHighlight = RowHighlight.None;
            varName.IsEditing   = false;
        }

        return list;
    }
}
