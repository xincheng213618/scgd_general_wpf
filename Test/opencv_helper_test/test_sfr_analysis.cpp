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

void verifyRobustLocalization()
{
    // Analytic Gaussian edge plus a pixel-scale bright-platform raster. The
    // raster is below the unchanged raw-platform noise limit, but its distant
    // derivatives used to bias the centroid enough to reject a straight edge.
    constexpr double sigma = .85, slope = .09;
    cv::Mat1d textured(144, 200);
    for (int y = 0; y < textured.rows; ++y) for (int x = 0; x < textured.cols; ++x) {
        const double distance = (x - 100.0 - slope * (y - 72.0)) / std::sqrt(1 + slope * slope);
        const double step = .5 * (1 + std::erf(distance / (std::sqrt(2.0) * sigma)));
        textured(y, x) = .1 + .8 * step + .04 * step * ((x + y) % 2 ? -1.0 : 1.0);
    }
    const cv::Mat original = textured.clone();
    const double reference50 = std::sqrt(2 * std::log(2.0)) / (2 * CV_PI * sigma);
    double worstError = 0, worstAngleError = 0;
    for (int width : {56, 82, 112, 160}) for (int dx : {-2, 0, 2}) for (int dy : {-2, 0, 2}) {
        const auto result = analyze(textured, {{"encoding", "linear"}}, {100 - width / 2 + dx, 8 + dy, width, 128});
        const auto& c = result["channels"][0];
        if (!c["valid"] || c["mtf50"].is_null()) throw std::runtime_error("textured straight edge failed after ROI translation/resize");
        worstError = std::max(worstError, std::abs(c["mtf50"].get<double>() - reference50));
        worstAngleError = std::max(worstAngleError, std::abs(c["angleDegrees"].get<double>() - std::atan(slope) * 180.0 / CV_PI));
        if (result["edgeLocalization"] != "lowpass_peak_v1") throw std::runtime_error("missing localization provenance");
    }
    require(worstError < .003, "36 textured ROI placements retain analytic Gaussian MTF50 accuracy within 0.003 cy/pixel");
    require(worstAngleError < .05, "textured ROI angle stays within 0.05 degrees of known geometry");
    require(cv::norm(original, textured, cv::NORM_INF) == 0, "localization filtering never modifies source pixels");

    // A smoothed localization image must not conceal physical edge irregularity.
    for (bool jagged : {false, true}) {
        cv::Mat1d irregular(128, 128);
        for (int y = 0; y < irregular.rows; ++y) for (int x = 0; x < irregular.cols; ++x) {
            const double displacement = jagged ? (y % 2 ? 1.0 : -1.0) : 2.0 * std::sin(2 * CV_PI * y / 64.0);
            const double distance = x - (63.5 + .1 * (y - 63.5) + displacement);
            irregular(y, x) = .1 + .8 * .5 * (1 + std::erf(distance / (std::sqrt(2.0) * 1.2)));
        }
        auto c = analyze(irregular)["channels"][0];
        require(invalid(c) && c["reason"] == "edge_fit_residual_too_large", "curved and row-jittered edges still fail the unchanged straight-edge residual limit");
    }
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
        verifyRobustLocalization();
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
        // Closing must not fill real background beside a complete target or
        // invent clipping. Containment is determined by the foreground itself.
        const auto bounds=found["targetRoi"];
        const int bx=bounds["x"], by=bounds["y"], bw=bounds["width"], bh=bounds["height"];
        for (int margin : {1, 2, 3, 4, 8}) {
            const RoiRect searches[] = {
                {bx-margin,0,480-bx+margin,480}, {0,by-margin,480,480-by+margin},
                {0,0,bx+bw+margin,480}, {0,0,480,by+bh+margin}
            };
            for (auto roi : searches) {
                auto nearBoundary=locate(target,roi);
                require(nearBoundary["located"],"BMW complete target remains locatable beside each search boundary");
                require(nearBoundary["targetRoi"]==bounds,"morphology padding preserves the original target bounds");
            }
        }
        const RoiRect clippedSearches[] = {
            {bx+4,0,480-bx-4,480}, {0,by+4,480,480-by-4},
            {0,0,bx+bw-4,480}, {0,0,480,by+bh-4}
        };
        for (auto roi : clippedSearches)
            require(!locate(target,roi)["located"],"BMW genuinely clipped sector is still rejected at each search boundary");
        // Camera resolution and search-box margins must not determine whether
        // shallow, slightly non-ideal printed edges produce enough Hough votes.
        for (int size : {320, 960, 1440}) {
            cv::Mat scaled;
            cv::resize(target, scaled, {size, size}, 0, 0, cv::INTER_CUBIC);
            cv::Mat1f mapX(size,size), mapY(size,size);
            for (int y=0;y<size;++y) for (int x=0;x<size;++x) {
                mapX(y,x)=static_cast<float>(x);
                mapY(y,x)=static_cast<float>(y+size*.002*std::sin(2*CV_PI*x/size));
            }
            cv::Mat warped;
            cv::remap(scaled,warped,mapX,mapY,cv::INTER_LINEAR,cv::BORDER_REPLICATE);
            cv::Mat surround(size+160,size+160,CV_8UC3,cv::Scalar(135,135,135));
            warped.copyTo(surround(cv::Rect(80,80,size,size)));
            for (int margin : {0, 24, 60}) {
                auto localized=locate(surround,{80-margin,80-margin,size+2*margin,size+2*margin});
                require(localized["located"],"BMW high-resolution axes survive mild contour bending and changed search margins");
                require(std::abs(localized["centerX"].get<double>()-(80+size*.5))<size*.01
                    && std::abs(localized["centerY"].get<double>()-(80+size*.5))<size*.01,
                    "normalized axis coordinates map back to the physical target center");
            }
        }
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
        auto locateChart=[](const cv::Mat& image, RoiRect roi, int type) {
            char* output=nullptr;
            int code=M_LocateSfrTargetV1(borrow(image),roi,type,&output);
            if(code<=0||!output) throw std::runtime_error("chart localization export failed");
            auto data=json::parse(std::string(output,code-1)); FreeResult(output); return data;
        };
        require(locateChart(target,{0,0,480,480},2)["chartType"]=="bmw","automatic mode preserves BMW shape recognition");
        require(locateChart(two,{0,0,960,480},2)["reason"]=="multiple_targets_in_search_roi","automatic mode does not reinterpret ambiguous BMW targets");
        auto checker=[](double degrees, int size=720, double spacing=180) {
            cv::Mat1b image(size,size);
            double angle=degrees*CV_PI/180;
            for(int y=0;y<image.rows;++y) for(int x=0;x<image.cols;++x) {
                double dx=x-size*.5,dy=y-size*.5;
                int u=static_cast<int>(std::floor((dx*std::cos(angle)+dy*std::sin(angle))/spacing)),
                    v=static_cast<int>(std::floor((-dx*std::sin(angle)+dy*std::cos(angle))/spacing));
                image(y,x)=(u+v)%2==0?40:200;
            }
            cv::GaussianBlur(image,image,{7,7},1.2);
            return image;
        };
        for(double degrees : {-5.,5.,12.}) {
            auto image=checker(degrees), before=image.clone();
            auto board=locateChart(image,{60,60,600,600},1);
            require(board["located"] && board["chartType"]=="checkerboard","checkerboard junction located at positive and negative chart rotations");
            require(std::abs(board["centerX"].get<double>()-360)<3 && std::abs(board["centerY"].get<double>()-360)<3,"checkerboard coordinates retain original-image offset");
            require(locateChart(image,{60,60,600,600},2)["chartType"]=="checkerboard","automatic mode recognizes checkerboard after BMW shape validation fails");
            for(int id=0;id<4;++id) {
                const auto e=board["edges"][id], r=e["roi"], s=e["supportRoi"];
                require(e["id"]==id,"checkerboard four-edge identity is stable");
                cv::Rect roi(r["x"],r["y"],r["width"],r["height"]), support(s["x"],s["y"],s["width"],s["height"]);
                require((roi&support)==roi,"checkerboard automatic ROI stays between junctions");
                auto result=analyze(image,{{"encoding","linear"}},{roi.x,roi.y,roi.width,roi.height});
                require(result["channels"].size()==1 && result["channels"][0]["valid"],"checkerboard uses unchanged original-pixel single-channel SFR");
            }
            require(cv::norm(image,before,cv::NORM_INF)==0,"checkerboard localization and measurement never modify source pixels");
            cv::Mat wide; image.convertTo(wide,CV_16U,257);
            require(locateChart(wide,{60,60,600,600},1)["located"],"16-bit checkerboard localization");
        }
        auto large=checker(5,1920,480);
        // A clear junction does not need 100 pixels on each branch: measurement
        // availability is determined independently for each safe 40x32 ROI.
        for (double degrees : {-5., 5., 12.}) {
            auto image = checker(degrees);
            auto compact = locateChart(image, {260,260,200,200}, 1);
            require(compact["located"] && std::abs(compact["centerX"].get<double>()-360)<3
                && std::abs(compact["centerY"].get<double>()-360)<3, "compact checkerboard crop retains the identified junction");
            for (auto e : compact["edges"]) {
                auto r=e["roi"], s=e["supportRoi"];
                cv::Rect roi(r["x"],r["y"],r["width"],r["height"]), support(s["x"],s["y"],s["width"],s["height"]);
                require(e["reason"]=="" && (roi&support)==roi
                    && analyze(image,{{"encoding","linear"}},{roi.x,roi.y,roi.width,roi.height})["channels"][0]["valid"],
                    "compact checkerboard keeps original SFR size and quality requirements");
            }
        }
        auto partial = locateChart(checker(5), {293,250,160,230}, 1);
        require(partial["located"], "a short branch does not hide a located checkerboard junction");
        int supported=0, unsupported=0;
        for (auto e : partial["edges"]) {
            if (e["reason"]=="checkerboard_insufficient_edge_support") {
                ++unsupported;
                require(e["roi"]["width"]==0 && e["roi"]["height"]==0, "unsupported branch never supplies a measurement rectangle");
            } else {
                ++supported;
                auto r=e["roi"];
                require(analyze(checker(5),{{"encoding","linear"}},{r["x"],r["y"],r["width"],r["height"]})["channels"][0]["valid"],
                    "supported branches still calculate when another branch is short");
            }
        }
        require(supported>0 && unsupported>0, "narrow off-center crop reports per-edge support instead of losing all four edges");
        auto largeBoard=locateChart(large,{120,120,1680,1680},1);
        require(largeBoard["located"] && std::abs(largeBoard["centerX"].get<double>()-960)<3 && std::abs(largeBoard["centerY"].get<double>()-960)<3,"downsampled detection maps checkerboard center back to original pixels");
        for(auto e:largeBoard["edges"]) {
            auto r=e["roi"], s=e["supportRoi"];
            cv::Rect roi(r["x"],r["y"],r["width"],r["height"]), support(s["x"],s["y"],s["width"],s["height"]);
            require((roi&support)==roi && analyze(large,{{"encoding","linear"}},{roi.x,roi.y,roi.width,roi.height})["channels"][0]["valid"],"large checkerboard uses original-pixel supported measurement boxes");
        }
        auto aligned=checker(0);
        auto board=locateChart(aligned,{60,60,600,600},1);
        require(board["located"],"aligned checkerboard may locate without passing SFR quality");
        for(auto e:board["edges"]) {
            auto r=e["roi"], c=analyze(aligned,{{"encoding","linear"}},{r["x"],r["y"],r["width"],r["height"]})["channels"][0];
            require(invalid(c)&&c["reason"]=="edge_angle_out_of_range","aligned checkerboard is not digitally rotated or given fabricated MTF");
        }
        auto ambiguousBoard=locateChart(aligned,{180,60,540,600},1);
        require(!ambiguousBoard["located"]&&ambiguousBoard["reason"]=="ambiguous_checkerboard_corners","equidistant checkerboard junctions require explicit placement");
        auto mono=checker(5);
        cv::Mat red, blue, color;
        cv::GaussianBlur(mono,red,{13,13},2.0); cv::GaussianBlur(mono,blue,{9,9},1.0);
        cv::merge(std::vector<cv::Mat>{blue,mono,red},color);
        auto colorBoard=locateChart(color,{60,60,600,600},2);
        require(colorBoard["located"],"RGB checkerboard locates through the same geometry path");
        for(auto e:colorBoard["edges"]) {
            auto r=e["roi"], channels=analyze(color,{{"encoding","linear"}},{r["x"],r["y"],r["width"],r["height"]})["channels"];
            require(channels.size()==4 && std::all_of(channels.begin(),channels.end(),[](const auto& c){return c["valid"].template get<bool>();}),"RGB checkerboard retains four independently calculated channels");
            require(channels[1]["mtf50"].get<double>()<channels[3]["mtf50"].get<double>() && channels[3]["mtf50"].get<double>()<channels[2]["mtf50"].get<double>(),"different RGB blur remains visible in channel MTF50");
        }
        for(int type : {1,2}) {
            cv::Mat1b negative(480,480,uchar(200));
            require(!locateChart(negative,{0,0,480,480},type)["located"],"blank image cannot become a checkerboard");
            cv::rectangle(negative,{80,80,320,320},cv::Scalar(40),-1);
            require(!locateChart(negative,{0,0,480,480},type)["located"],"single rectangle cannot become a checkerboard junction");
            negative.setTo(200); cv::line(negative,{40,240},{440,240},40,20); cv::line(negative,{240,40},{240,440},40,20);
            require(!locateChart(negative,{0,0,480,480},type)["located"],"ordinary cross cannot become alternating checkerboard quadrants");
        }
        output=reinterpret_cast<char*>(1);
        require(M_LocateSfrTargetV1(borrow(target),{0,0,480,480},3,&output)<0&&output==nullptr,"unknown chart type rejected and output cleared");
        return true;
    } catch(const std::exception& ex) { std::cerr<<"BMW failure: "<<ex.what()<<'\n'; return false; }
}
