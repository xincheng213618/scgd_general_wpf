package com.colorvision.xcviewer;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

final class DeviceHealthPresentation {
    static final String DATA_SCOPE = "状态来自设备实际 MQTT 运行状态的固定归类；"
            + "不返回设备名称、编号、标识、地址、Topic、配置、原始状态载荷、时间戳或测量数据，"
            + "也不会执行设备操作。";

    private DeviceHealthPresentation() {
    }

    static ViewModel from(JSONObject payload) {
        if (payload == null || !payload.optBoolean("available", false)) {
            return new ViewModel(
                    false,
                    false,
                    true,
                    "当前无法读取检测设备状态",
                    "不会自动重连或重启设备",
                    "",
                    "请在电脑端检查设备注册表后再刷新。",
                    "",
                    0,
                    0,
                    Collections.emptyList());
        }

        boolean hasConfiguredDevices = payload.optBoolean("hasConfiguredDevices", false);
        int attentionCount = payload.optInt("attentionCount", 0);
        int busyCount = payload.optInt("busyCount", 0);
        int closedCount = payload.optInt("closedCount", 0);
        String headline;
        String guidance;
        if (!hasConfiguredDevices) {
            headline = "尚未发现已加载的检测设备";
            guidance = "请先在电脑端确认设备配置已加载。";
        } else if (attentionCount > 0) {
            headline = attentionCount + " 台设备需要关注";
            guidance = "请在电脑端核对对应设备类型的进程、授权和连接状态。";
        } else if (busyCount > 0) {
            headline = busyCount + " 台设备正在工作";
            guidance = "当前无需远程处置，可按需刷新状态。";
        } else if (closedCount > 0) {
            headline = closedCount + " 台设备已关闭";
            guidance = "已关闭不等同故障；如非预期，请在电脑端确认设备配置。";
        } else {
            headline = "检测设备状态正常";
            guidance = "当前无需远程处置，可按需刷新状态。";
        }

        List<Category> categories = new ArrayList<>();
        JSONArray values = payload.optJSONArray("categories");
        if (values != null) {
            for (int index = 0; index < values.length(); index++) {
                JSONObject category = values.optJSONObject(index);
                if (category == null) {
                    continue;
                }
                int unavailable = category.optInt("unavailableCount", 0);
                int unknown = category.optInt("unknownCount", 0);
                categories.add(new Category(
                        categoryLabel(category.optString("category", "other")),
                        stateSummary(category),
                        category.optInt("totalCount", 0),
                        unavailable > 0 || unknown > 0));
            }
        }

        List<Category> orderedCategories = new ArrayList<>(categories.size());
        for (Category category : categories) {
            if (category.attentionRequired) {
                orderedCategories.add(category);
            }
        }
        int attentionCategoryCount = orderedCategories.size();
        for (Category category : categories) {
            if (!category.attentionRequired) {
                orderedCategories.add(category);
            }
        }
        if (attentionCount > 0 && attentionCategoryCount > 0) {
            guidance = "优先检查 "
                    + categoryLabels(orderedCategories, attentionCategoryCount)
                    + "；请在电脑端核对对应类型的进程、授权和连接状态。";
        }

        return new ViewModel(
                true,
                hasConfiguredDevices,
                attentionCount > 0,
                headline,
                hasConfiguredDevices
                        ? "共 " + payload.optInt("totalCount", 0) + " 台 · " + stateSummary(payload)
                        : "当前没有可汇总的检测设备",
                unavailableReasonSummary(payload),
                guidance,
                payload.optString("observedAt", ""),
                attentionCount,
                attentionCategoryCount,
                Collections.unmodifiableList(orderedCategories));
    }

    static String stateSummary(JSONObject source) {
        List<String> states = new ArrayList<>();
        states.add("就绪 " + source.optInt("readyCount", 0));
        addCount(states, "忙碌", source.optInt("busyCount", 0));
        addCount(states, "切换中", source.optInt("transitioningCount", 0));
        addCount(states, "已关闭", source.optInt("closedCount", 0));
        addCount(states, "不可用", source.optInt("unavailableCount", 0));
        addCount(states, "未知", source.optInt("unknownCount", 0));
        return String.join(" · ", states);
    }

    private static String unavailableReasonSummary(JSONObject source) {
        List<String> reasons = new ArrayList<>();
        addCount(reasons, "离线", source.optInt("offlineCount", 0));
        addCount(reasons, "未初始化", source.optInt("uninitializedCount", 0));
        addCount(reasons, "未授权", source.optInt("unauthorizedCount", 0));
        addCount(reasons, "未归类", source.optInt("unclassifiedUnavailableCount", 0));
        return String.join(" · ", reasons);
    }

    private static void addCount(List<String> values, String label, int count) {
        if (count > 0) {
            values.add(label + " " + count);
        }
    }

    private static String categoryLabel(String value) {
        switch (value) {
            case "camera": return "相机类";
            case "algorithm": return "算法类";
            case "spectrum": return "光谱类";
            case "instrument": return "仪表与供电类";
            case "motion": return "运动控制类";
            case "calibration": return "校准类";
            default: return "其他设备";
        }
    }

    private static String categoryLabels(List<Category> categories, int count) {
        List<String> labels = new ArrayList<>();
        for (int index = 0; index < count && index < categories.size(); index++) {
            labels.add(categories.get(index).label);
        }
        return String.join("、", labels);
    }

    static final class ViewModel {
        final boolean available;
        final boolean hasConfiguredDevices;
        final boolean attentionRequired;
        final String headline;
        final String summary;
        final String unavailableReasons;
        final String guidance;
        final String observedAt;
        final int attentionCount;
        final int attentionCategoryCount;
        final List<Category> categories;

        ViewModel(
                boolean available,
                boolean hasConfiguredDevices,
                boolean attentionRequired,
                String headline,
                String summary,
                String unavailableReasons,
                String guidance,
                String observedAt,
                int attentionCount,
                int attentionCategoryCount,
                List<Category> categories) {
            this.available = available;
            this.hasConfiguredDevices = hasConfiguredDevices;
            this.attentionRequired = attentionRequired;
            this.headline = headline;
            this.summary = summary;
            this.unavailableReasons = unavailableReasons;
            this.guidance = guidance;
            this.observedAt = observedAt;
            this.attentionCount = Math.max(0, attentionCount);
            this.attentionCategoryCount = Math.max(
                    0, Math.min(attentionCategoryCount, categories.size()));
            this.categories = categories;
        }

        List<Category> attentionCategories() {
            return categories.subList(0, attentionCategoryCount);
        }

        List<Category> otherCategories() {
            return categories.subList(attentionCategoryCount, categories.size());
        }

        String attentionCategorySummary() {
            return categoryLabels(categories, attentionCategoryCount);
        }

        String compactAttentionSummary() {
            if (!attentionRequired) {
                return "";
            }
            String categorySummary = compactAttentionCategorySummary();
            if (categorySummary.isEmpty()) {
                return attentionCount > 0
                        ? "需关注 " + attentionCount : headline;
            }
            String reasonSummary = compactUnavailableReasonSummary();
            if (reasonSummary.isEmpty()) {
                reasonSummary = "状态未知 " + Math.max(1, attentionCount);
            }
            return categorySummary + " · " + reasonSummary;
        }

        String compactAttentionActionSummary() {
            return attentionCategoryCount > 0
                    ? compactAttentionSummary()
                    : attentionCount > 0 ? "设备需关注 " + attentionCount + " 个" : headline;
        }

        boolean canTrackRecovery() {
            return available && attentionCount > 0;
        }

        private String compactAttentionCategorySummary() {
            List<String> labels = new ArrayList<>();
            int visibleCount = Math.min(2, attentionCategoryCount);
            for (int index = 0; index < visibleCount; index++) {
                String label = categories.get(index).label;
                labels.add(label.endsWith("类")
                        ? label.substring(0, label.length() - 1) : label);
            }
            String result = String.join("、", labels);
            return attentionCategoryCount > visibleCount
                    ? result + "等 " + attentionCategoryCount + " 类" : result;
        }

        private String compactUnavailableReasonSummary() {
            if (unavailableReasons.isEmpty()) {
                return "";
            }
            String[] reasons = unavailableReasons.split(" · ");
            if (reasons.length == 1) {
                return reasons[0];
            }
            return reasons[0] + "、" + reasons[1] + (reasons.length > 2 ? "等" : "");
        }

        String accessibilitySummary() {
            String reasons = unavailableReasons.isEmpty()
                    ? "" : "。不可用原因，" + unavailableReasons;
            String affected = attentionCategoryCount == 0
                    ? "" : "。需关注类型，" + attentionCategorySummary();
            return headline + "。" + summary + reasons + affected;
        }
    }

    static final class Category {
        final String label;
        final String summary;
        final int totalCount;
        final boolean attentionRequired;

        Category(String label, String summary, int totalCount, boolean attentionRequired) {
            this.label = label;
            this.summary = summary;
            this.totalCount = totalCount;
            this.attentionRequired = attentionRequired;
        }

        String accessibilityLabel() {
            return label + "，共 " + totalCount + " 台，" + summary.replace(" · ", "，");
        }
    }
}
