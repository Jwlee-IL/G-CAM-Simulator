namespace Gcam.Studio.Services;

/// <summary>The trusted SHA-256 of the embedded readout offer / cost record
/// (<c>samples/evidence/studio-readout/offer-cost-v1.json</c>). Compiled in, so a changed record cannot be loaded without a
/// visible code change; <c>samples/evidence/studio_readout_offer_record.py --check</c> verifies it against the record.</summary>
public static class ReadoutOfferPin
{
    public const string Sha256 = "24b3bf6714e9b856a9b8aaac8d95c2bdbd212659808172440716e15fdeda8b6a";
}
