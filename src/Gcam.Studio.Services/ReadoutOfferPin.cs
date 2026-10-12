namespace Gcam.Studio.Services;

/// <summary>The trusted SHA-256 of the embedded readout offer / cost record
/// (<c>samples/evidence/studio-readout/offer-cost-v1.json</c>). Compiled in, so a changed record cannot be loaded without a
/// visible code change; <c>samples/evidence/studio_readout_offer_record.py --check</c> verifies it against the record.</summary>
public static class ReadoutOfferPin
{
    public const string Sha256 = "e12cf8135759b7d669cdb5cbfacef7bc650a74cf0ac2e91a31933796feb2ff5f";
}
