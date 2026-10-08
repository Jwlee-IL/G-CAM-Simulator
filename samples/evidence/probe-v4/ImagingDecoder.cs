using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using Gcam.Decoding;
static class ExactCount {
 // Poisson sums preserve the law; bounded-mean Knuth avoids a large-lambda approximation.
 public static int Sample(IRandom rng,int mean) {
  int total=0;
  while(mean>0) {
   int block=Math.Min(mean,20);double limit=Math.Exp(-block),product=1;int k=0;
   do {k++;product*=rng.NextDouble();}while(product>limit);
   total+=k-1;mean-=block;
  }
  return total;
 }
}

// Exact correlation coefficients cached once. Double precision and engine peak interpolation.
class FastDecoder {
 readonly double[][] weights; readonly int size; readonly double origin,step; readonly SubCellMethod method;
 public FastDecoder(SimulationConfig cfg) {
  var geo=DefaultSimulationFactory.ReconstructionGeometry(cfg);origin=-geo.ReconHalfExtentMm;step=geo.ReconStepMm;
  size=(int)Math.Round(2*geo.ReconHalfExtentMm/step)+1; method=cfg.Decoder.SubCellInterpolation;
  int n=cfg.Detector.PixelsX; weights=new double[size*size][];
  var unit=new DetectorImage(n,n); var engine=(CrossCorrelationDecoder)new DefaultSimulationFactory().CreateDecoder(cfg)!;
  for(int i=0;i<weights.Length;i++) weights[i]=new double[n*n];
  for(int k=0;k<n*n;k++) {
   unit[k%n,k/n]=1;
   for(int gy=0;gy<size;gy++)for(int gx=0;gx<size;gx++) weights[gy*size+gx][k]=engine.PointResponse(unit,origin+gx*step,origin+gy*step);
   unit[k%n,k/n]=0;
  }
 }
 public (double X,double Y) Decode(DetectorImage image) {
  var pixels=new double[image.Width*image.Height];for(int k=0;k<pixels.Length;k++)pixels[k]=image[k%image.Width,k/image.Width];
  var recon=new DetectorImage(size,size);
  for(int i=0;i<weights.Length;i++) {
   double sum=0;var w=weights[i];int k=0;
   for(;k<=pixels.Length-System.Numerics.Vector<double>.Count;k+=System.Numerics.Vector<double>.Count)
    sum+=System.Numerics.Vector.Dot(new System.Numerics.Vector<double>(pixels,k),new System.Numerics.Vector<double>(w,k));
   for(;k<pixels.Length;k++)sum+=pixels[k]*w[k];recon[i%size,i/size]=sum;
  }
  var (bx,by)=PeakInterpolation.Argmax(recon);var (dx,dy)=PeakInterpolation.Estimate(recon,bx,by,method);
  return (origin+(bx+dx)*step,origin+(by+dy)*step);
 }
}
