#ifndef NOMINMAX
#define NOMINMAX
#endif
#include "../../Native/include/opencv_media_export.h"
#include <nlohmann/json.hpp>
#include <opencv2/opencv.hpp>
#include <cmath>
#include <iostream>
#include <limits>
#include <stdexcept>

namespace {
using nlohmann::json;
HImage borrow(const cv::Mat& image)
{
    HImage h{};
    h.rows = image.rows; h.cols = image.cols; h.channels = image.channels();
    h.depth = CvDepthToHImageDepth(image.depth()); h.stride = static_cast<int>(image.step);
    h.isDispose = true; h.pData = image.data;
    return h;
}
json analyze(const cv::Mat& image, json options = {{"encoding", "linear"}}, RoiRect roi = {})
{
    char* output = nullptr;
    const std::string config = options.dump();
    int code = M_AnalyzeSfrV2(borrow(image), roi, config.c_str(), &output);
    if (code <= 0 || !output) throw std::runtime_error("SFR API error " + std::to_string(code));
    const std::string copy(output, code - 1);
    FreeResult(output);
    return json::parse(copy);
}
cv::Mat edge(double sigma = 1.2, double slope = .1)
{
    cv::Mat1d image(128, 128);
    for (int y = 0; y < image.rows; ++y) for (int x = 0; x < image.cols; ++x) {
        const double distance = (x - (63.5 + slope * (y - 63.5))) / std::sqrt(1 + slope * slope);
        image(y, x) = .1 + .8 * .5 * (1 + std::erf(distance / (std::sqrt(2.0) * sigma)));
    }
    return image;
}
void require(bool condition, const char* label)
{
    if (!condition) throw std::runtime_error(label);
    std::cout << "[SFR V2 PASS] " << label << '\n';
}
bool invalid(const json& c)
{
    return !c.at("valid").get<bool>() && c.at("mtf50").is_null() && c.at("mtf10").is_null()
        && c.at("frequencies").empty() && c.at("mtf").empty();
}
}

bool RunSfrAnalysisTests()
{
    try {
        for (double value : {0.0, .5, 1.0})
            require(invalid(analyze(cv::Mat(128, 128, CV_64F, cv::Scalar(value)))["channels"][0]), "flat image has no numerical measurement");
        const cv::Mat linear = edge();
        auto result = analyze(linear);
        const auto& c = result["channels"][0];
        require(c["valid"] && c["channel"] == "L" && result["nyquist"] == .5, "Gaussian edge accepted with explicit input-pixel units");
        const double reference50 = std::sqrt(2 * std::log(2.0)) / (2 * CV_PI * 1.2);
        // 4x edge binning + finite window is a numerical approximation to this analytic Gaussian.
        require(std::abs(c["mtf50"].get<double>() - reference50) < .003, "Gaussian MTF50 agrees with analytic transfer function within 0.003 cy/pixel");
        require(c["edgePositions"].size() == c["esf"].size() && c["lsfPositions"].size() == c["lsf"].size(), "ESF and LSF own coordinates preserved");
        require(result == analyze(linear), "same pixels and configuration are deterministic");
        auto sharp = analyze(edge(.35))["channels"][0];
        require(sharp["valid"] && sharp["mtf50"].is_null() && sharp["mtf10"].is_null(), "missing crossings never replaced by 0.495");
        cv::Mat reverse = 1.0 - linear, rotated;
        cv::rotate(linear, rotated, cv::ROTATE_90_CLOCKWISE);
        const auto inverted = analyze(reverse)["channels"][0], horizontal = analyze(rotated)["channels"][0];
        require(inverted["valid"] && horizontal["valid"] && horizontal["rotated"], "edge polarity and horizontal orientation accepted");
        require(std::abs(inverted["mtf50"].get<double>() - c["mtf50"].get<double>()) < 1e-8
            && std::abs(horizontal["mtf50"].get<double>() - c["mtf50"].get<double>()) < 1e-8, "polarity and rotation do not change Gaussian response");
        cv::Mat u16, f32; linear.convertTo(u16, CV_16U, 65535); linear.convertTo(f32, CV_32F);
        require(std::abs(analyze(u16)["channels"][0]["mtf50"].get<double>() - c["mtf50"].get<double>()) < .0001
            && std::abs(analyze(f32)["channels"][0]["mtf50"].get<double>() - c["mtf50"].get<double>()) < .0001, "16-bit and float preserve the same radiometric response");
        cv::Mat1d encoded = linear.clone();
        for (auto& v : encoded) v = v <= .0031308 ? 12.92 * v : 1.055 * std::pow(v, 1.0 / 2.4) - .055;
        require(std::abs(analyze(encoded, {{"encoding", "srgb"}})["channels"][0]["mtf50"].get<double>() - c["mtf50"].get<double>()) < 1e-8, "sRGB decoding restores linear response");
        cv::Mat color; cv::merge(std::vector<cv::Mat>{linear, linear, linear}, color);
        auto rgb = analyze(color, {{"encoding", "linear"}, {"displayTarget", true}});
        require(rgb["channels"].size() == 4 && rgb["channels"][0]["valid"] && rgb["channels"][3]["valid"], "smooth RGB display edge remains measurable with a display-chain warning");
        cv::Mat3d raster = color.clone();
        for (int y = 0; y < raster.rows; ++y) for (int x = 0; x < raster.cols; ++x)
            for (int channel = 0; channel < 3; ++channel) raster(y, x)[channel] *= (x % 3 == channel ? 1.0 : .15);
        auto textured = analyze(raster);
        require(invalid(textured["channels"][0]) && invalid(textured["channels"][1]), "resolved RGB raster cannot silently pass as a smooth single edge");
        require(invalid(analyze(edge(1.2, 0))["channels"][0]), "axis-aligned edge rejected for inadequate slant");
        cv::Mat1d noise(128, 128); cv::RNG rng(42); rng.fill(noise, cv::RNG::NORMAL, 0, .01);
        cv::Mat low = (linear - .1) * .0375 + .45 + noise;
        require(invalid(analyze(low)["channels"][0]), "low contrast noisy edge rejected instead of producing an unstable high score");
        char* output = reinterpret_cast<char*>(1);
        require(M_AnalyzeSfrV2(borrow(linear), {-1,0,40,40}, "{}", &output) < 0 && output == nullptr, "invalid ROI rejected and output cleared");
        require(M_AnalyzeSfrV2(borrow(linear), {}, "{", &output) < 0 && output == nullptr, "malformed configuration rejected");
        require(M_AnalyzeSfrV2(borrow(linear), {}, "{}", nullptr) < 0, "null output pointer rejected");
        cv::Mat1d nanImage = linear.clone(); nanImage(64, 64) = std::numeric_limits<double>::quiet_NaN();
        require(M_AnalyzeSfrV2(borrow(nanImage), {}, "{}", &output) < 0 && output == nullptr, "non-finite source pixel rejected in Release build");
        return true;
    }
    catch (const std::exception& ex) { std::cerr << "SFR V2 test failed: " << ex.what() << '\n'; return false; }
}

bool RunBmwLocalizationTests()
{
    try {
        auto locate=[](const cv::Mat& image,RoiRect roi) {
            char* output=nullptr;
            int code=M_LocateBmwTargetV1(borrow(image),roi,&output);
            if(code<=0||!output) throw std::runtime_error("BMW localization export failed");
            auto data=json::parse(std::string(output,code-1)); FreeResult(output); return data;
        };
        cv::Mat3b target(480,480,cv::Vec3b(220,220,220));
        const double angle=5*CV_PI/180;
        for(int y=0;y<target.rows;++y) for(int x=0;x<target.cols;++x) {
            double dx=x-240.,dy=y-240.,u=dx*std::cos(angle)+dy*std::sin(angle),v=-dx*std::sin(angle)+dy*std::cos(angle);
            if(u*u+v*v<180*180&&u*v>0) target(y,x)=cv::Vec3b(25,25,25);
        }
        cv::GaussianBlur(target,target,{5,5},1);
        auto found=locate(target,{0,0,480,480});
        require(found["located"],"BMW opposed sectors located");
        require(found["edges"].size()==4,"BMW retains four fixed edges");
        for(int id=0;id<4;++id) {
            auto e=found["edges"][id],r=e["roi"];
            require(e["id"]==id,"BMW edge identity stable");
            auto result=analyze(target,{{"encoding","linear"}},{r["x"],r["y"],r["width"],r["height"]});
            require(result["channels"].size()==4,"BMW edge uses RGB and L diagnostics");
            for(auto c:result["channels"]) require(c["valid"],"synthetic BMW straight segment valid");
        }
        cv::Mat wide;
        target.convertTo(wide,CV_16U,257);
        require(locate(wide,{0,0,480,480})["located"],"BMW 16-bit localization");
        target.convertTo(wide,CV_64F,1.0/255);
        require(locate(wide,{0,0,480,480})["located"],"BMW double RGB localization without changing measurement pixels");
        require(!locate(target,{160,0,320,480})["located"],"BMW incomplete target rejected");
        cv::Mat shifted(600,650,CV_8UC3,cv::Scalar(220,220,220)); target.copyTo(shifted(cv::Rect(90,60,480,480)));
        auto offset=locate(shifted,{90,60,480,480});
        require(std::abs(offset["centerX"].get<double>()-found["centerX"].get<double>()-90)<1e-6,"BMW original image X offset");
        require(std::abs(offset["centerY"].get<double>()-found["centerY"].get<double>()-60)<1e-6,"BMW original image Y offset");
        cv::Mat blank(480,480,CV_8UC3,cv::Scalar(220,220,220));
        require(!locate(blank,{0,0,480,480})["located"],"BMW blank rejected");
        cv::rectangle(blank,{80,80,320,320},cv::Scalar(25,25,25),-1);
        require(!locate(blank,{0,0,480,480})["located"],"BMW rectangle rejected");
        blank.setTo(cv::Scalar(220,220,220)); cv::circle(blank,{240,240},180,cv::Scalar(25,25,25),-1);
        require(!locate(blank,{0,0,480,480})["located"],"BMW circle rejected");
        blank.setTo(cv::Scalar(220,220,220));
        cv::line(blank,{70,240},{410,240},cv::Scalar(25,25,25),24);
        cv::line(blank,{240,70},{240,410},cv::Scalar(25,25,25),24);
        require(!locate(blank,{0,0,480,480})["located"],"BMW ordinary cross rejected");
        cv::Mat two(480,960,CV_8UC3); target.copyTo(two(cv::Rect(0,0,480,480))); target.copyTo(two(cv::Rect(480,0,480,480)));
        auto ambiguous=locate(two,{0,0,960,480});
        require(!ambiguous["located"]&&ambiguous["reason"]=="multiple_targets_in_search_roi","BMW ambiguity rejected");
        char* output=reinterpret_cast<char*>(1);
        require(M_LocateBmwTargetV1(borrow(target),{},&output)<0&&output==nullptr,"BMW full-frame sentinel prohibited");
        require(M_LocateBmwTargetV1(borrow(target),{-1,0,100,100},&output)<0&&output==nullptr,"BMW invalid ROI clears output");
        return true;
    } catch(const std::exception& ex) { std::cerr<<"BMW failure: "<<ex.what()<<'\n'; return false; }
}
