#include "sfr_slanted.h"
#include <Eigen/Dense>
#include <algorithm>
#include <cmath>
#include <stdexcept>

namespace cvcore {
namespace sfr {

namespace {

constexpr int DEFAULT_FACTOR = 4;
constexpr double PI = 3.141592653589793238462643383279502884;

struct OrientedImage {
    cv::Mat image;
    bool rotated = false;
};

struct EsfBin {
    double count = 0.0;
    double sum = 0.0;
};

struct SfrSignalWorkspace {
    std::vector<EsfBin> bins;
    std::vector<double> esf;
    std::vector<double> lsf;
    std::vector<double> mtf;
    cv::Mat fft;
};

double fir2fixValue(int index, int n, int m)
{
    if (index <= 0) {
        return 1.0;
    }

    const int order = m - 1;
    double x = PI * index * order / (2.0 * (n + 1));
    double denom = std::sin(x);

    if (std::abs(denom) < 1e-12) {
        return 10.0;
    }

    return std::clamp(std::abs(x / denom), 1.0, 10.0);
}

void centerShiftInPlace(std::vector<double>& values, int center)
{
    const int n = static_cast<int>(values.size());
    if (n == 0) {
        return;
    }

    const int mid = static_cast<int>(std::round((n + 1) / 2.0));
    const int del = static_cast<int>(std::round(center - mid));

    if (del > 0) {
        if (del >= n) {
            std::fill(values.begin(), values.end(), 0.0);
            return;
        }

        std::move(values.begin() + del, values.end(), values.begin());
        std::fill(values.end() - del, values.end(), 0.0);
        return;
    }

    if (del < 0) {
        const int shift = -del;
        if (shift >= n) {
            std::fill(values.begin(), values.end(), 0.0);
            return;
        }

        std::move_backward(values.begin(), values.end() - shift, values.end());
        std::fill(values.begin(), values.begin() + shift, 0.0);
    }
}

// Strictly corresponds to MATLAB rotatev2 for single channel (mm = 1 case)
OrientedImage orientEdgeVertical(const cv::Mat& src) {
    cv::Mat a;
    if (src.channels() == 1) {
        a = src;
    }
    else {
        cv::extractChannel(src, a, 1);
    }

    int nlin = a.rows;
    int npix = a.cols;

    int nn = 3;
    if (nlin <= nn || npix <= nn) {
        return { src, false };
    }

    // MATLAB: testv = abs(mean(a(end-nn,:,mm))-mean(a(nn,:,mm)));
    double v_top = cv::mean(a.row(nn - 1))[0];
    double v_bottom = cv::mean(a.row(nlin - nn - 1))[0];
    double testv = std::abs(v_bottom - v_top);

    // MATLAB: testh = abs(mean(a(:,end-nn,mm))-mean(a(:,nn,mm)));
    double h_left = cv::mean(a.col(nn - 1))[0];
    double h_right = cv::mean(a.col(npix - nn - 1))[0];
    double testh = std::abs(h_right - h_left);

    cv::Mat out;
    if (testv > testh) {
        cv::rotate(src, out, cv::ROTATE_90_COUNTERCLOCKWISE);
        return { out, true };
    }

    return { src, false };
}

cv::Mat applyOrientation(const cv::Mat& src, bool rotated)
{
    if (!rotated) {
        return src;
    }

    cv::Mat out;
    cv::rotate(src, out, cv::ROTATE_90_COUNTERCLOCKWISE);
    return out;
}

cv::Mat1d asMat1d(const cv::Mat& src)
{
    if (src.empty()) {
        return {};
    }
    if (src.type() == CV_64FC1) {
        return src;
    }

    cv::Mat1d out;
    src.convertTo(out, CV_64F);
    return out;
}

double tukeySymmetricValue(int index, int length, double alpha)
{
    if (length <= 0) {
        return 0.0;
    }
    if (length == 1 || alpha == 0.0) {
        return 1.0;
    }

    alpha = std::clamp(alpha, 0.0, 1.0);
    const double midpoint = (length - 1) / 2.0;
    const int mirroredIndex = std::min(index, length - 1 - index);
    if (mirroredIndex <= alpha * midpoint) {
        return 0.5 * (1.0 + std::cos(PI * (mirroredIndex / (alpha * midpoint) - 1.0)));
    }

    return 1.0;
}

double tukey2Value(int index, int n, double alpha, double mid)
{
    if (n <= 0) {
        return 0.0;
    }

    if (n < 3) {
        return 1.0;
    }

    if (mid <= 0.0) {
        mid = n / 2.0;
    }
    if (alpha <= 0.0) {
        alpha = 1.0;
    }

    const double m1 = n / 2.0;
    const double m2 = mid;
    const double m3 = n - mid;
    const double mm = std::max(m2, m3);

    int n2 = static_cast<int>(std::round(2.0 * mm));
    if (n2 < n) {
        n2 = n;
    }

    const int sourceStart = mid >= m1 ? 0 : n2 - n;
    return tukeySymmetricValue(sourceStart + index, n2, alpha);
}

double weightedTukey2Value(int index, int n, double alpha, double mid)
{
    return 0.95 * tukey2Value(index, n, alpha, mid) + 0.05;
}

std::vector<double> makeWeightedTukey2(int n, double alpha, double mid)
{
    std::vector<double> weights(n);
    for (int i = 0; i < n; ++i) {
        weights[i] = weightedTukey2Value(i, n, alpha, mid);
    }
    return weights;
}

double row_centroid(const double* row, const double* weights, int n) {
    double num = 0.0;
    double den = 0.0;

    for (int j = 0; j < n; ++j) {
        double v = row[j] * weights[j];
        num += v * (j + 1);
        den += v;
    }

    if (den == 0.0) {
        return 0.0;
    }

    return num / den - 0.5;
}

// Calculate centroid of windowed derivative (column direction)
// Corresponds to MATLAB: centroid(c.*win) - 0.5
double row_centroid(const double* row, int n, double alpha, double mid) {
    double num = 0.0;
    double den = 0.0;

    for (int j = 0; j < n; ++j) {
        double v = row[j] * weightedTukey2Value(j, n, alpha, mid);
        num += v * (j + 1);
        den += v;
    }

    if (den == 0.0) {
        return 0.0;
    }

    return num / den - 0.5;
}

// Convert scaled polynomial coefficients to unscaled
std::vector<double> polyfit_convert_cpp(const double* p_scaled_asc, int coefficientCount, double m, double s) {
    if (s == 0.0) {
        throw std::runtime_error("Standard deviation cannot be zero.");
    }

    const int degree = coefficientCount - 1;
    if (degree < 0) return {};

    std::vector<double> p_unscaled(degree + 1, 0.0);
    std::vector<double> basis(degree + 1, 0.0);
    std::vector<double> nextBasis(degree + 1, 0.0);
    basis[0] = 1.0;

    const double invS = 1.0 / s;
    const double negMeanOverS = -m * invS;
    for (int i = 0; i <= degree; ++i) {
        const double p_i = p_scaled_asc[i];
        for (int j = 0; j <= i; ++j) {
            p_unscaled[j] += p_i * basis[j];
        }

        if (i == degree) {
            break;
        }

        std::fill(nextBasis.begin(), nextBasis.end(), 0.0);
        for (int j = 0; j <= i; ++j) {
            nextBasis[j] += basis[j] * negMeanOverS;
            nextBasis[j + 1] += basis[j] * invS;
        }
        basis.swap(nextBasis);
    }

    return p_unscaled;
}

class RowPolynomialFitter {
public:
    RowPolynomialFitter(int rowCount, int degree)
        : rowCount_(rowCount)
    {
        if (rowCount_ <= 0) {
            return;
        }

        degree_ = std::max(0, degree);
        if (degree_ + 1 > rowCount_) {
            degree_ = rowCount_ - 1;
        }

        mean_ = 0.5 * static_cast<double>(rowCount_ - 1);
        stddev_ = std::sqrt(static_cast<double>(rowCount_) * static_cast<double>(rowCount_ + 1) / 12.0);
        if (stddev_ == 0.0) {
            return;
        }

        Eigen::MatrixXd design(rowCount_, degree_ + 1);
        for (int i = 0; i < rowCount_; ++i) {
            const double x = (static_cast<double>(i) - mean_) / stddev_;
            double xp = 1.0;
            for (int j = 0; j <= degree_; ++j) {
                design(i, j) = xp;
                xp *= x;
            }
        }

        qr_.compute(design);
        valid_ = true;
    }

    std::vector<double> fit(const std::vector<double>& samples) const
    {
        if (!valid_ || static_cast<int>(samples.size()) != rowCount_) {
            return {};
        }

        Eigen::Map<const Eigen::VectorXd> y(samples.data(), samples.size());
        Eigen::VectorXd coeff = qr_.solve(y);
        return polyfit_convert_cpp(coeff.data(), static_cast<int>(coeff.size()), mean_, stddev_);
    }

    bool isValid() const
    {
        return valid_;
    }

private:
    int rowCount_ = 0;
    int degree_ = 0;
    double mean_ = 0.0;
    double stddev_ = 0.0;
    bool valid_ = false;
    Eigen::HouseholderQR<Eigen::MatrixXd> qr_;
};

struct SlantedEdgeFitWorkspace {
    int rows = 0;
    int cols = 0;
    double alpha = 1.0;
    std::vector<double> centerWeights;
    std::vector<double> loc;
    cv::Mat1d derivative;
    RowPolynomialFitter edgeFitter;
    RowPolynomialFitter slopeFitter;

    SlantedEdgeFitWorkspace(int rowCount, int colCount, int polynomialDegree)
        : rows(rowCount)
        , cols(colCount)
        , centerWeights(makeWeightedTukey2(colCount, alpha, (colCount + 1) / 2.0))
        , edgeFitter(rowCount, polynomialDegree)
        , slopeFitter(rowCount, 1)
    {
    }

    bool isValidFor(const cv::Mat& mat) const
    {
        return mat.rows == rows && mat.cols == cols && rows >= 2 && cols >= 6 &&
            !centerWeights.empty() && edgeFitter.isValid() && slopeFitter.isValid();
    }
};

double polyvalScalar(double x, const std::vector<double>& coeff)
{
    double value = 0.0;
    for (auto it = coeff.rbegin(); it != coeff.rend(); ++it) {
        value = value * x + *it;
    }
    return value;
}

// Equivalent to MATLAB: b = deriv1(a, nlin, npix, fil) for the fixed 1x2 edge kernel.
void horizontalDerivative(const cv::Mat1d& a, bool reverseGradient, cv::Mat1d& dst) {
    const int nlin = a.rows;
    const int npix = a.cols;

    dst.create(nlin, npix);
    if (npix <= 0) {
        return;
    }

    const double scale = reverseGradient ? -0.5 : 0.5;
    for (int i = 0; i < nlin; ++i) {
        const double* src = a.ptr<double>(i);
        double* rowDst = dst.ptr<double>(i);

        if (npix == 1) {
            rowDst[0] = -scale * src[0];
            continue;
        }

        if (npix == 2) {
            const double edge = -scale * src[1];
            rowDst[0] = edge;
            rowDst[1] = edge;
            continue;
        }

        for (int j = 1; j < npix - 1; ++j) {
            rowDst[j] = scale * (src[j + 1] - src[j]);
        }

        rowDst[0] = rowDst[1];
        rowDst[npix - 1] = rowDst[npix - 2];
    }
}

// Polynomial edge fitting for single channel ROI
std::vector<double> poly_edge_fit(const cv::Mat& mat,
                                  SlantedEdgeFitWorkspace& workspace) {
    CV_Assert(mat.channels() == 1);
    const int nlin = mat.rows;
    const int npix = mat.cols;
    if (!workspace.isValidFor(mat)) {
        return {};
    }

    cv::Mat1d a = asMat1d(mat);

    double tleft = cv::sum(a.colRange(0, 5))[0];
    double tright = cv::sum(a.colRange(npix - 5, npix))[0];

    horizontalDerivative(a, tleft > tright, workspace.derivative);

    workspace.loc.assign(nlin, 0.0);
    auto& loc = workspace.loc;

    for (int i = 0; i < nlin; ++i) {
        loc[i] = row_centroid(workspace.derivative.ptr<double>(i), workspace.centerWeights.data(), npix);
    }

    auto coeff = workspace.edgeFitter.fit(loc);

    // Second pass: use fitted position for asymmetric window
    for (int i = 0; i < nlin; ++i) {
        double place = polyvalScalar(static_cast<double>(i), coeff);
        loc[i] = row_centroid(workspace.derivative.ptr<double>(i), npix, workspace.alpha, place);
    }

    coeff = workspace.edgeFitter.fit(loc);
    return coeff;
}

bool computeEsf(const cv::Mat& mat,
                const std::vector<double>& fitme,
                int nbin,
                SfrSignalWorkspace& workspace) {
    CV_Assert(mat.channels() == 1);
    const int nlin = mat.rows;
    const int npix = mat.cols;
    if (nbin <= 0) nbin = DEFAULT_FACTOR;

    const int nn = static_cast<int>(std::floor(npix * nbin));
    if (nn <= 0) {
        workspace.esf.clear();
        return false;
    }

    const double slope = (fitme.size() > 1) ? fitme[1] : 0.0;
    int offset = 0;
    if (std::abs(slope) > 1e-8) {
        double invslope = 1.0 / slope;
        offset = static_cast<int>(std::round(nbin * (0.0 - (nlin - 1) / invslope)));
    }
    int del = std::abs(offset);
    if (offset > 0) offset = 0;

    const int bwidth = nn + del + 150;
    workspace.bins.assign(bwidth, EsfBin{});
    auto& bins = workspace.bins;

    cv::Mat1d a = asMat1d(mat);
    for (int m = 0; m < nlin; ++m) {
        const double rowOffset = polyvalScalar(static_cast<double>(m), fitme) - fitme[0];
        const double* row = a.ptr<double>(m);
        int rawLing = static_cast<int>(std::ceil(-rowOffset * static_cast<double>(nbin))) + 1 - offset;
        for (int n = 0; n < npix; ++n) {
            int ling = std::clamp(rawLing, 0, bwidth - 1);
            bins[ling].count += 1.0;
            bins[ling].sum += row[n];
            rawLing += nbin;
        }
    }

    int start = 1 + static_cast<int>(std::round(0.5 * del));
    for (int i = start; i < start + nn && i < bwidth; ++i) {
        if (bins[i].count == 0.0) {
            int i1 = std::max(start, i - 1);
            int i2 = std::min(start + nn - 1, i + 1);
            bins[i].count = 0.5 * (bins[i1].count + bins[i2].count);
            bins[i].sum = 0.5 * (bins[i1].sum + bins[i2].sum);
        }
    }

    workspace.esf.assign(nn, 0.0);
    for (int i = 0; i < nn; ++i) {
        int idx = start + i;
        if (idx >= 0 && idx < bwidth && bins[idx].count > 0.0) {
            workspace.esf[i] = bins[idx].sum / bins[idx].count;
        }
    }
    return true;
}

bool computeLsf(const std::vector<double>& esf, SfrSignalWorkspace& workspace) {
    const int n = static_cast<int>(esf.size());
    if (n < 2) {
        workspace.lsf.clear();
        return false;
    }
    if (n == 2) {
        workspace.lsf.assign(2, 0.5 * esf[0]);
        return true;
    }

    workspace.lsf.assign(n, 0.0);
    auto& result = workspace.lsf;
    for (int i = 1; i < n - 1; ++i) {
        result[i] = 0.5 * esf[i - 1] - 0.5 * esf[i + 1];
    }
    result[0] = result[1];
    result[n - 1] = result[n - 2];

    if (result[0] == 0.0) result[0] = result[1];
    else if (result[n - 1] == 0.0) result[n - 1] = result[n - 2];

    double maxVal = 0.0;
    int maxIdx = 0;
    for (int i = 0; i < n; ++i) {
        if (std::abs(result[i]) > std::abs(maxVal)) {
            maxVal = result[i];
            maxIdx = i;
        }
    }

    centerShiftInPlace(result, maxIdx + 1);

    for (int i = 0; i < n; ++i) {
        result[i] *= tukeySymmetricValue(i, n, 1.0);
    }
    return true;
}

bool computeMtf(const std::vector<double>& lsf, SfrSignalWorkspace& workspace) {
    const int nn = static_cast<int>(lsf.size());
    if (nn <= 0) {
        workspace.mtf.clear();
        return false;
    }

    cv::Mat lsfMat(1, nn, CV_64F, const_cast<double*>(lsf.data()));

    cv::dft(lsfMat, workspace.fft, cv::DFT_COMPLEX_OUTPUT);

    cv::Vec2d dc = workspace.fft.at<cv::Vec2d>(0, 0);
    const double DC = std::hypot(dc[0], dc[1]);
    const int nn2 = nn / 2 + 1;
    workspace.mtf.assign(nn2, 0.0);
    if (DC <= 1e-12) {
        return true;
    }

    for (int i = 0; i < nn2; ++i) {
        cv::Vec2d c = workspace.fft.at<cv::Vec2d>(0, i);
        double raw = std::hypot(c[0], c[1]) / DC;
        workspace.mtf[i] = raw * fir2fixValue(i, nn2, 3);
    }
    return true;
}

double find_freq_at_threshold(
    const std::vector<double>& freq_axis,
    const std::vector<double>& sfr_data,
    double threshold)
{
    if (freq_axis.size() != sfr_data.size() || sfr_data.empty()) {
        return 0.0;
    }

    auto it = std::adjacent_find(sfr_data.begin(), sfr_data.end(),
        [=](double y1, double y2) { return y1 >= threshold && y2 < threshold; });

    if (it == sfr_data.end()) {
        return std::isfinite(freq_axis.back()) ? freq_axis.back() : 0.0;
    }

    const auto index = std::distance(sfr_data.begin(), it);
    const int i = static_cast<int>(index);
    double y1 = sfr_data[i];
    double y2 = sfr_data[i + 1];
    double x1 = freq_axis[i];
    double x2 = freq_axis[i + 1];

    if (std::abs(y2 - y1) < 1e-9) {
        return x1;
    }
    return x1 + (threshold - y1) * (x2 - x1) / (y2 - y1);
}

int normalizedBinning(int binning)
{
    return binning > 0 ? binning : DEFAULT_FACTOR;
}

bool isPositiveFinite(double value)
{
    return std::isfinite(value) && value > 0.0;
}

cv::Mat normalizeTo8U(const cv::Mat& img)
{
    if (img.empty()) {
        return {};
    }

    if (img.depth() == CV_8U) {
        return img;
    }

    cv::Mat source = img;
    cv::Mat temp32;
    if (img.depth() == CV_64F) {
        img.convertTo(temp32, CV_MAKETYPE(CV_32F, img.channels()));
        source = temp32;
    }

    if (source.depth() == CV_32F) {
        if (source.data == img.data) {
            source = source.clone();
        }
        cv::Mat scalarView = source.reshape(1);
        cv::patchNaNs(scalarView, 0.0);
    }

    cv::Mat out;
    cv::normalize(source, out, 0, 255, cv::NORM_MINMAX, CV_8U);
    return out;
}

cv::Mat toLuminance(const cv::Mat& img)
{
    if (img.empty()) {
        return {};
    }
    if (img.channels() == 1) {
        return img;
    }

    if (img.channels() != 3 && img.channels() != 4) {
        return {};
    }

    cv::Mat blue;
    cv::Mat green;
    cv::Mat red;
    cv::extractChannel(img, blue, 0);
    cv::extractChannel(img, green, 1);
    cv::extractChannel(img, red, 2);
    blue.convertTo(blue, CV_64F);
    green.convertTo(green, CV_64F);
    red.convertTo(red, CV_64F);

    return 0.072 * blue + 0.715 * green + 0.213 * red;
}

struct SlantedEdgeModel {
    std::vector<double> fit;
    double edgeSlope = 0.0;
    int rows = 0;
    bool rotated = false;
    bool valid = false;
};

SlantedEdgeModel fitSlantedEdgeModel(const cv::Mat& gray,
                                     double requestedSlope,
                                     bool rotated,
                                     SlantedEdgeFitWorkspace& workspace)
{
    SlantedEdgeModel model;
    if (gray.empty() || gray.channels() != 1 || gray.rows < 2 || gray.cols < 6) {
        return model;
    }
    if (!(requestedSlope == -1.0 || std::isfinite(requestedSlope))) {
        return model;
    }
    if (!workspace.isValidFor(gray)) {
        return model;
    }

    auto fitme = poly_edge_fit(gray, workspace);
    if (fitme.empty() || workspace.loc.size() < 2) {
        return model;
    }

    auto fitme1 = workspace.slopeFitter.fit(workspace.loc);
    if (fitme1.size() < 2) {
        return model;
    }

    double edgeSlope = requestedSlope;
    if (edgeSlope == -1.0) {
        edgeSlope = fitme1[1];
    }
    if (!std::isfinite(edgeSlope)) {
        return model;
    }

    int nlin = gray.rows;
    double s = std::abs(edgeSlope);
    int nlin1 = nlin;
    if (s > 1e-12) {
        nlin1 = static_cast<int>(std::round(std::floor(nlin * s) / s));
    }
    if (nlin1 <= 0 || nlin1 > nlin) {
        nlin1 = nlin;
    }

    model.fit = std::move(fitme);
    model.edgeSlope = edgeSlope;
    model.rows = nlin1;
    model.rotated = rotated;
    model.valid = true;
    return model;
}

SlantedEdgeModel buildSlantedEdgeModel(const cv::Mat& grayInput,
                                       int polynomialDegree,
                                       double requestedSlope)
{
    if (grayInput.empty() || grayInput.channels() != 1 || grayInput.rows < 2 || grayInput.cols < 6) {
        return {};
    }

    OrientedImage oriented = orientEdgeVertical(grayInput);
    SlantedEdgeFitWorkspace workspace(oriented.image.rows, oriented.image.cols, polynomialDegree);
    return fitSlantedEdgeModel(oriented.image, requestedSlope, oriented.rotated, workspace);
}

SFRResult calculateFromModel(const cv::Mat& channelInput,
                             const SlantedEdgeModel& model,
                             double pixelPitch,
                             int binning,
                             double frequencySlope,
                             SfrSignalWorkspace& workspace)
{
    SFRResult result;
    if (!model.valid || channelInput.empty() || channelInput.channels() != 1 ||
        !isPositiveFinite(pixelPitch) || !std::isfinite(frequencySlope)) {
        return result;
    }

    binning = normalizedBinning(binning);

    cv::Mat channel = applyOrientation(channelInput, model.rotated);
    if (channel.empty() || channel.rows < model.rows || model.rows <= 0) {
        return result;
    }
    if (model.rows < channel.rows) {
        channel = channel.rowRange(0, model.rows);
    }

    const double adjustedPitch = pixelPitch * std::cos(std::atan(frequencySlope));
    if (!isPositiveFinite(adjustedPitch)) {
        return result;
    }

    const double samplePitch = adjustedPitch / static_cast<double>(binning);
    if (!isPositiveFinite(samplePitch)) {
        return result;
    }

    if (!computeEsf(channel, model.fit, binning, workspace)) return result;

    if (!computeLsf(workspace.esf, workspace)) return result;

    if (!computeMtf(workspace.lsf, workspace)) return result;

    int nn = static_cast<int>(workspace.esf.size());
    int nn2 = nn / 2 + 1;
    double freqlim = 1.0;
    int nn2out = static_cast<int>(std::round(nn2 * freqlim / 2.0));
    nn2out = std::min<int>(nn2out, static_cast<int>(workspace.mtf.size()));
    if (nn2out <= 0) {
        return result;
    }

    result.freq.resize(nn2out);
    result.sfr.resize(nn2out);
    for (int i = 0; i < nn2out; ++i) {
        double f = static_cast<double>(i) / (samplePitch * nn);
        result.freq[i] = f;
        result.sfr[i] = workspace.mtf[i];
    }

    double mtf10_cypix = find_freq_at_threshold(result.freq, result.sfr, 0.1);
    double mtf50_cypix = find_freq_at_threshold(result.freq, result.sfr, 0.5);
    double hs = 0.495 / pixelPitch;
    double fNyquist = 0.5 / pixelPitch;

    result.edgeSlope = model.edgeSlope;
    result.mtf10_cypix = std::min(mtf10_cypix, hs);
    result.mtf50_cypix = std::min(mtf50_cypix, hs);
    result.mtf10_norm = (fNyquist > 0) ? result.mtf10_cypix / fNyquist : 0.0;
    result.mtf50_norm = (fNyquist > 0) ? result.mtf50_cypix / fNyquist : 0.0;

    return result;
}

} // namespace

SFRResult calculateSlantedEdgeSFR(const cv::Mat& imgIn,
                                  double pixelPitch,
                                  int polynomialDegree,
                                  int binning,
                                  double edgeSlope)
{
    cv::Mat source = normalizeTo8U(imgIn);
    cv::Mat gray = asMat1d(toLuminance(source));
    SlantedEdgeModel model = buildSlantedEdgeModel(gray, polynomialDegree, edgeSlope);
    SfrSignalWorkspace workspace;
    return calculateFromModel(gray, model, pixelPitch, binning, model.edgeSlope, workspace);
}

SFRMultiChannelResult calculateSlantedEdgeSFRMultiChannel(const cv::Mat& imgIn,
                                                          double pixelPitch,
                                                          int polynomialDegree,
                                                          int binning,
                                                          double edgeSlope)
{
    SFRMultiChannelResult result;
    cv::Mat source = normalizeTo8U(imgIn);
    if (source.empty()) {
        return result;
    }

    if (source.channels() == 3 || source.channels() == 4) {
        OrientedImage oriented = orientEdgeVertical(source);
        cv::Mat orientedSource = oriented.image;

        std::vector<cv::Mat> channels;
        cv::split(orientedSource, channels);
        if (channels.size() < 3) {
            return result;
        }

        cv::Mat blue = asMat1d(channels[0]);
        cv::Mat green = asMat1d(channels[1]);
        cv::Mat red = asMat1d(channels[2]);
        cv::Mat gray = 0.072 * blue + 0.715 * green + 0.213 * red;

        SlantedEdgeFitWorkspace workspace(orientedSource.rows, orientedSource.cols, polynomialDegree);
        SlantedEdgeModel luminanceModel = fitSlantedEdgeModel(gray, edgeSlope, false, workspace);
        SlantedEdgeModel redModel = fitSlantedEdgeModel(red, edgeSlope, false, workspace);
        SlantedEdgeModel greenModel = fitSlantedEdgeModel(green, edgeSlope, false, workspace);
        SlantedEdgeModel blueModel = fitSlantedEdgeModel(blue, edgeSlope, false, workspace);

        if (!luminanceModel.valid || !redModel.valid || !greenModel.valid || !blueModel.valid) {
            return result;
        }

        const double frequencySlope = luminanceModel.edgeSlope;
        SfrSignalWorkspace signalWorkspace;
        result.red = calculateFromModel(red, redModel, pixelPitch, binning, frequencySlope, signalWorkspace);
        result.green = calculateFromModel(green, greenModel, pixelPitch, binning, frequencySlope, signalWorkspace);
        result.blue = calculateFromModel(blue, blueModel, pixelPitch, binning, frequencySlope, signalWorkspace);
        result.luminance = calculateFromModel(gray, luminanceModel, pixelPitch, binning, frequencySlope, signalWorkspace);
        result.channelCount = result.red.isValid()
            && result.green.isValid()
            && result.blue.isValid()
            && result.luminance.isValid() ? 4 : 0;
        return result;
    }

    if (source.channels() == 1) {
        cv::Mat gray = asMat1d(source);
        SlantedEdgeModel model = buildSlantedEdgeModel(gray, polynomialDegree, edgeSlope);
        SfrSignalWorkspace signalWorkspace;
        result.luminance = calculateFromModel(gray, model, pixelPitch, binning, model.edgeSlope, signalWorkspace);
        result.channelCount = result.luminance.isValid() ? 1 : 0;
    }

    return result;
}

} // namespace sfr
} // namespace cvcore
