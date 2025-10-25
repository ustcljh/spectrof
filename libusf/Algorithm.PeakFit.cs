using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace libusf_cs
{
    public class PeakFit
    {
        public static double GaussianPeak(double x, double center, double width, double height)
        {
            double arg = (x - center) / width;
            if (arg * arg > 100) return 0; // avoid overflow
            return height * Math.Exp(-0.5 * arg * arg);
        }

        // Lorentzian Peak
        public static double LorentzianPeak(double x, double center, double width, double height)
        {
            double arg = (x - center) / width;
            return height / (1.0 + arg * arg);
        }

        // The erf, complex form
        public static Complex ComplexErf(Complex z)
        {
            // erf(z) = 1 - exp(-z^2) * (a1*t + a2*t^2 + a3*t^3 + a4*t^4 + a5*t^5), where t = 1 / (1 + p*z)
            // Works well for both real and complex z
            double p = 0.3275911;
            double a1 = 0.254829592, a2 = -0.284496736, a3 = 1.421413741,
                   a4 = -1.453152027, a5 = 1.061405429;

            Complex t = 1.0 / (1.0 + p * z);
            Complex expTerm = Complex.Exp(-z * z);

            Complex poly = (((((a5 * t + a4) * t + a3) * t + a2) * t + a1) * t);
            return 1.0 - expTerm * poly;
        }

        // Voigt Peak (approximation using Humlíček formula)
        public static double VoigtPeak(double x, double center, double gaussianWidth, double lorentzWidth, double height)
        {
            double sigma = gaussianWidth / Math.Sqrt(2.0 * Math.Log(2.0)); // convert FWHM to sigma if needed
            double gamma = lorentzWidth / 2.0; // half width at half maximum
            double x0 = (x - center) / sigma;
            double y0 = gamma / sigma;

            // Approximation of the real part of the Faddeeva function
            Complex z = new Complex(x0, y0);
            Complex w = Complex.Exp(-z * z) * (1.0 - ComplexErf(-Complex.ImaginaryOne * z));

            double voigt = (height / (sigma * Math.Sqrt(2 * Math.PI))) * w.Real;
            return voigt;
        }


        public class Peak { public double Center, Width, Height; public Peak(double c, double w, double h) { Center = c; Width = w; Height = h; } }

        static double MultiFunc(double x, List<Peak> peaks, double baseline, PeakFunction peakfun)
        {
            double y = baseline;
            foreach (var p in peaks) y += peakfun(x, p.Center, p.Width, p.Height);
            return y;
        }

        // Simple moving average smoothing
        static double[] Smooth(double[] y, int windowSize = 3)
        {
            double[] smoothed = new double[y.Length];
            int half = windowSize / 2;
            for (int i = 0; i < y.Length; i++)
            {
                int start = Math.Max(i - half, 0);
                int end = Math.Min(i + half, y.Length - 1);
                double sum = 0;
                for (int j = start; j <= end; j++)
                    sum += y[j];
                smoothed[i] = sum / (end - start + 1);
            }
            return smoothed;
        }

        // Detect peaks in smoothed data
        static List<int> DetectPeaks(double[] y, double threshold = 0.05)
        {
            List<int> indices = new List<int>();
            for (int i = 1; i < y.Length - 1; i++)
            {
                if (y[i] > y[i - 1] && y[i] > y[i + 1] && y[i] > threshold)
                    indices.Add(i);
            }
            return indices;
        }

        // Estimate initial peaks using smoothed data
        public static List<Peak> EstimateInitialPeaks(double[] x, double[] y, int windowSize = 3)
        {
            double baseline = y.Min();
            double[] yAdj = y.Select(v => v - baseline).ToArray();

            // Smooth the adjusted data
            double[] ySmooth = Smooth(yAdj, windowSize);

            // Detect peaks in smoothed data
            var peakIndices = DetectPeaks(ySmooth, 0.05 * ySmooth.Max());

            List<Peak> peaks = new List<Peak>();
            double dx = x.Max() - x.Min();
            double minWidth = dx / x.Length;

            foreach (int i in peakIndices)
            {
                double center = x[i];
                double height = yAdj[i];

                // Estimate width using half-maximum
                double halfMax = height / 2.0;
                int left = i, right = i;
                while (left > 0 && ySmooth[left] > halfMax) left--;
                while (right < y.Length - 1 && ySmooth[right] > halfMax) right++;
                double width = (x[right] - x[left]) / 2.0;
                if (width < minWidth) width = minWidth;

                peaks.Add(new Peak(center, width, height));
            }

            return peaks;
        }

        public static List<Peak> EstimateInitialPeaks(double[] x, double[] y)
        {
            double baseline = y.Min();
            double[] yAdj = y.Select(v => v - baseline).ToArray();
            var indices = DetectPeaks(yAdj, 0.05 * yAdj.Max());
            var peaks = new List<Peak>();
            double dx = x.Max() - x.Min();
            double minWidth = dx / x.Length; // reasonable min width

            foreach (int i in indices)
            {
                double center = x[i];
                double height = yAdj[i];

                // width estimate
                double half = height / 2;
                int left = i, right = i;
                while (left > 0 && yAdj[left] > half) left--;
                while (right < y.Length - 1 && yAdj[right] > half) right++;
                double width = (x[right] - x[left]) / 2.0;
                if (width < minWidth) width = minWidth;

                peaks.Add(new Peak(center, width, height));
            }
            return peaks;
        }

        static double[] ComputeResiduals(double[] x, double[] y, List<Peak> peaks, double baseline, PeakFunction peakfun)
        {
            double[] r = new double[x.Length];
            for (int i = 0; i < x.Length; i++)
                r[i] = MultiFunc(x[i], peaks, baseline, peakfun) - y[i];
            return r;
        }

        public delegate double PeakFunction(double x, double center, double width, double height);

        public static void FitPeaks(double[] x, double[] y, List<Peak> peaks, ref double baseline, PeakFunction peakfun, int maxIter = 100, double lambdaInit = 0.01)
        {
            int nPeaks = peaks.Count;
            double lambda = lambdaInit;
            double minWidth = (x.Max() - x.Min()) / x.Length;

            for (int iter = 0; iter < maxIter; iter++)
            {
                double[] r = ComputeResiduals(x, y, peaks, baseline, peakfun);
                double err = r.Select(v => v * v).Sum();

                // Jacobian using central differences
                double[,] J = new double[x.Length, nPeaks * 3 + 1];
                double delta = 1e-5;

                for (int i = 0; i < nPeaks; i++)
                {
                    var p = peaks[i];

                    // Center derivative
                    double orig = p.Center;
                    p.Center = orig + delta; var r1 = ComputeResiduals(x, y, peaks, baseline, peakfun);
                    p.Center = orig - delta; var r2 = ComputeResiduals(x, y, peaks, baseline, peakfun);
                    for (int j = 0; j < x.Length; j++) J[j, i * 3 + 0] = (r1[j] - r2[j]) / (2 * delta);
                    p.Center = orig;

                    // Width derivative
                    orig = p.Width;
                    p.Width = Math.Max(orig + delta, minWidth); r1 = ComputeResiduals(x, y, peaks, baseline, peakfun);
                    p.Width = Math.Max(orig - delta, minWidth); r2 = ComputeResiduals(x, y, peaks, baseline, peakfun);
                    for (int j = 0; j < x.Length; j++) J[j, i * 3 + 1] = (r1[j] - r2[j]) / (2 * delta);
                    p.Width = orig;

                    // Height derivative
                    orig = p.Height;
                    p.Height = orig + delta; r1 = ComputeResiduals(x, y, peaks, baseline, peakfun);
                    p.Height = orig - delta; r2 = ComputeResiduals(x, y, peaks, baseline, peakfun);
                    for (int j = 0; j < x.Length; j++) J[j, i * 3 + 2] = (r1[j] - r2[j]) / (2 * delta);
                    p.Height = orig;
                }

                // Baseline derivative
                double baselineOrig = baseline;
                baseline = baseline + delta; var r1b = ComputeResiduals(x, y, peaks, baseline, peakfun);
                baseline = baseline - delta; var r2b = ComputeResiduals(x, y, peaks, baseline, peakfun);
                for (int j = 0; j < x.Length; j++) J[j, nPeaks * 3] = (r1b[j] - r2b[j]) / (2 * delta);
                baseline = baselineOrig;

                // JTJ and JTr
                int pCount = nPeaks * 3 + 1;
                double[,] JTJ = new double[pCount, pCount];
                double[] JTr = new double[pCount];
                for (int i = 0; i < pCount; i++)
                {
                    for (int j = 0; j < pCount; j++)
                    {
                        double sum = 0;
                        for (int k = 0; k < x.Length; k++) sum += J[k, i] * J[k, j];
                        if (i == j) sum *= (1 + lambda);
                        JTJ[i, j] = sum;
                    }
                    double sum2 = 0;
                    for (int k = 0; k < x.Length; k++) sum2 += J[k, i] * r[k];
                    JTr[i] = sum2;
                }

                // Solve linear system
                double[] dp = SolveLinearSystem(JTJ, JTr.Select(v => -v).ToArray());

                Console.WriteLine($"Iteration {iter}");

                for (int i = 0; i < nPeaks; i++)
                {
                    peaks[i].Center = Math.Clamp(peaks[i].Center + dp[i * 3 + 0], x.Min(), x.Max());
                    peaks[i].Width = Math.Clamp(peaks[i].Width + dp[i * 3 + 1], minWidth, (x.Max() - x.Min()));
                    peaks[i].Height = Math.Max(peaks[i].Height + dp[i * 3 + 2], 0);

                    Console.WriteLine($"Peak {i}, center {peaks[i].Center}, width {peaks[i].Width}, height {peaks[i].Height}");
                }
                baseline = Math.Max(baseline + dp[nPeaks * 3], 0);

                // Optional: reduce lambda if error decreases
                double errNew = ComputeResiduals(x, y, peaks, baseline, peakfun).Select(v => v * v).Sum();
                if (errNew < err) lambda *= 0.7; else lambda *= 2;
            }
        }

        public static double[] SolveLinearSystem(double[,] A, double[] b)
        {
            int n = b.Length;
            double[,] M = new double[n, n + 1];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++) M[i, j] = A[i, j];
                M[i, n] = b[i];
            }

            // Gaussian elimination
            for (int i = 0; i < n; i++)
            {
                int pivot = i;
                for (int k = i + 1; k < n; k++) if (Math.Abs(M[k, i]) > Math.Abs(M[pivot, i])) pivot = k;
                for (int j = i; j <= n; j++) { double tmp = M[i, j]; M[i, j] = M[pivot, j]; M[pivot, j] = tmp; }
                for (int k = i + 1; k < n; k++)
                {
                    double f = M[k, i] / M[i, i];
                    for (int j = i; j <= n; j++) M[k, j] -= f * M[i, j];
                }
            }

            double[] x = new double[n];
            for (int i = n - 1; i >= 0; i--)
            {
                x[i] = M[i, n] / M[i, i];
                for (int k = i - 1; k >= 0; k--) M[k, n] -= M[k, i] * x[i];
            }
            return x;
        }
    }
}
