using System.Globalization;
using System.Security.Cryptography;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Simulation;

namespace Gcam.Tests;

/// <summary>Captured at the unchanged TODO-42 baseline before production edits; complete bits and suffix progression.</summary>
public sealed class ReadoutFieldIdentityTests
{
    private static string Hash(Action<BinaryWriter> action)
    {
        using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory);
        action(writer); writer.Flush(); return Convert.ToHexString(SHA256.HashData(memory.ToArray()));
    }
    private static readonly string[] Fixtures =
    [
        "12345,False,0,0,0,2D771BC7343982D2AC1B3D5AA5E8834335D7E19B8CE8CE8A37695DA415C81106",
        "12345,False,0,0.1,0,240E167EDA5A3F21CAC5D8CF65CFA0800D033E227A9028F44D9EF44C9407F9C3",
        "12345,False,0,1,0,FA15FE03391431F5894E61DCA8AF3C93865D383ECB0301D948BF2EA65CBE38F9",
        "12345,False,1,0,0,2D771BC7343982D2AC1B3D5AA5E8834335D7E19B8CE8CE8A37695DA415C81106",
        "12345,False,1,0.1,0,7C58FFC0C1BCA3EA070F1D90244F9506EBB5B5095B015F1CAB029646A44C0419",
        "12345,False,1,1,0,F62B464507393D6AA4993EA7B5C4895F7DFB07002EBA397810A9393A9F936BFF",
        "12345,True,0,0,0,00050AD1B265FD85C08A68858663588E51898A764962DA7D3E54789B9917AA88",
        "12345,True,0,0,1,616CCFF02CE60B587F92D0C22AE042FF39A9DF9D5A25688438A937C491292983",
        "12345,True,0,0.1,0,5BF79629642D99A88744ED7164F58C8881411D0C373633DF45CD699FB76F4516",
        "12345,True,0,0.1,1,7675B1426DB5512CCC13BA0436EE7E097866CD4587570BCF78538FED8D9B6CEC",
        "12345,True,0,1,0,5619319728CCA6D04E62CD9B295A8A44FF5A5B0F6F2263E1F50BA0728F99C717",
        "12345,True,0,1,1,7141F123A0AD0BC579178C87F3CDBDC615ECDC0720613AF0D8E8350E264CE830",
        "12345,True,1,0,0,00050AD1B265FD85C08A68858663588E51898A764962DA7D3E54789B9917AA88",
        "12345,True,1,0,1,616CCFF02CE60B587F92D0C22AE042FF39A9DF9D5A25688438A937C491292983",
        "12345,True,1,0.1,0,4A7D023FDE69AC5E30B1407E74106A4B4E0DF4D0EC1412822FFCC3F6ED560B5F",
        "12345,True,1,0.1,1,018667BECC2E0C39BD3C3ED25161AFF7ABBFDFEF600D3BF1F5EE3E0A508F8794",
        "12345,True,1,1,0,EFCBAD84AFC1F9894E6C85D8F85F8C74111C79832FC9743B26C33C5FB622787D",
        "12345,True,1,1,1,CC973F0FC45764491B21601E71C22F698EF3C4CF4A4CEF4C51F4798BE867FD03",
        "4711,False,0,0,0,2D771BC7343982D2AC1B3D5AA5E8834335D7E19B8CE8CE8A37695DA415C81106",
        "4711,False,0,0.1,0,F8C752B0F71A7C1C67715EF7978A08742C0179E276B76D09137406D90B44E483",
        "4711,False,0,1,0,597D8D58C34C65896DD4E31DDFBBC2E7FA593D5F75E71E20C14AC64527BED3A1",
        "4711,False,1,0,0,2D771BC7343982D2AC1B3D5AA5E8834335D7E19B8CE8CE8A37695DA415C81106",
        "4711,False,1,0.1,0,E74543ABD420C0D0D62E1AD5BC7A1AD09B2EF59F35647E557BC4376318CA17C7",
        "4711,False,1,1,0,E7A8B2CB0B9810C5E331AD5FBE36C24187FA9FA77C7BB19A3CE617256A0C6DDE",
        "4711,True,0,0,0,5BB67CC1B7D248A986251B227196E7F55E273FDCA24BE450940902AA3C169DD5",
        "4711,True,0,0,1,EE2D3F8C6EA65000FFB9BC10F50457744222228A4952BF85C2F36199B8A809FB",
        "4711,True,0,0.1,0,90ECE67CE954E4E50B256D0BD9F8FAA60B633D5EABBD211041B5ACA6E1659310",
        "4711,True,0,0.1,1,70B7777A82671AA03B8106CA9444AA3D87225824E02F9070989062B4673C9833",
        "4711,True,0,1,0,6598FAAEFDEA6350456038335544CC0FC530C815EAD10F3E2A01203F3914FAA7",
        "4711,True,0,1,1,F315BB6904DF95B5BE33533507067F6219806AC3984741095B294359484C1C56",
        "4711,True,1,0,0,5BB67CC1B7D248A986251B227196E7F55E273FDCA24BE450940902AA3C169DD5",
        "4711,True,1,0,1,EE2D3F8C6EA65000FFB9BC10F50457744222228A4952BF85C2F36199B8A809FB",
        "4711,True,1,0.1,0,A3313EB7C16C3AA8752A2935FBAD9A43081BCEBF31CC099B3DC46FF4D8175043",
        "4711,True,1,0.1,1,80C47DC67C76B76BBA47E6E5CAF91AB30D6EB40644E54E1B2CD7FC6C95FA924C",
        "4711,True,1,1,0,D69E09093A4777E9EABA942BA6C125672956AE4E6B9FB9CDA3E798F1491E7E44",
        "4711,True,1,1,1,E442B7DF27080587D4D99F7812B382D46D69D3F71EB4027907BDED0A52D22B86",
        "place,12345,2165B432F5C3EC04EA2D03FD6C22B8A3B4599E1286297CC981E09F4B4DEC4ABC",
        "place,4711,DEE7DC11A6C2161C5DC10D6CFD70AF8A4FBE03CA159AB38E23B200F870F86CF1",
    ];

    [Fact]
    public void DirectAmbientSignalBsrDarkAndPlace_MatchPrechangeBitsAndProgression()
    {
        foreach (var row in Fixtures)
        {
            var fields = row.Split(',');
            if (fields[0] == "place")
            {
                var type = typeof(ListModeSource).Assembly.GetType("Gcam.Simulation.ListModeBackground")!;
                var c = new SimulationConfig { Seed = int.Parse(fields[1]), Background = new() { BackgroundToSignalRatio = 1 } };
                var background = Activator.CreateInstance(type, c)!;
                var place = type.GetMethod("Place")!;
                Assert.Equal(fields[2], Hash(w =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        var e = (DetectedEvent)place.Invoke(background, [i % 2 == 0 ? 3.0 : 200.0, i * .1])!;
                        w.Write(e.PixelX); w.Write(e.PixelY); w.Write(e.DepositKeV); w.Write(e.ArrivalTimeS);
                    }
                }));
                continue;
            }
            var config = new SimulationConfig { Seed = int.Parse(fields[0]) };
            if (!bool.Parse(fields[1])) config.Source.ActivityBq = 0;
            config.Detector.ReflectorGapMm = .1; config.Detector.EntranceAbsorberMm = .15; config.Detector.BackingScatterMm = 2;
            config.Ambient = new() { Geometry = (AmbientGeometry)int.Parse(fields[2]),
                DoseRateMicroSvPerHour = double.Parse(fields[3], CultureInfo.InvariantCulture) };
            double bsr = double.Parse(fields[4], CultureInfo.InvariantCulture);
            if (bsr > 0) config.Background = new() { BackgroundToSignalRatio = bsr, DarkCountRateKcps = .001 };
            using var source = new ListModeSource(config);
            Assert.Equal(fields[5], Hash(w =>
            {
                for (int i = 1; i <= 44; i++)
                    while (source.ArrivalTimeS < i * .25)
                    {
                        var next = source.AdvanceUntil(i * .25);
                        w.Write(next.HasValue); w.Write(source.ArrivalTimeS);
                        if (next is { } e) { w.Write(e.PixelX); w.Write(e.PixelY); w.Write(e.DepositKeV); w.Write(e.ArrivalTimeS); }
                    }
                w.Write(source.HistoriesEmitted); w.Write(source.HistoriesDetected); w.Write(source.EventsAccepted);
                w.Write(source.DetectedWeight); w.Write(source.SourceRateCps); w.Write(source.AmbientRateCps); w.Write(source.RateCps);
            }));
        }
    }
}
