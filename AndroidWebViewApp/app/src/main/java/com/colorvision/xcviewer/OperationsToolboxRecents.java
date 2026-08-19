package com.colorvision.xcviewer;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

final class OperationsToolboxRecents {
    static final int MAX_ITEMS = 4;
    private static final int MAX_ACTION_ID_LENGTH = 96;

    private OperationsToolboxRecents() {
    }

    static List<String> parse(String serialized) {
        List<String> actionIds = new ArrayList<>();
        if (serialized == null || serialized.trim().isEmpty()) {
            return Collections.emptyList();
        }
        for (String value : serialized.split("\\n")) {
            String actionId = normalize(value);
            if (isValidActionId(actionId) && !actionIds.contains(actionId)) {
                actionIds.add(actionId);
                if (actionIds.size() == MAX_ITEMS) {
                    break;
                }
            }
        }
        return Collections.unmodifiableList(actionIds);
    }

    static String record(String serialized, String actionId) {
        String normalizedActionId = normalize(actionId);
        List<String> recent = new ArrayList<>(parse(serialized));
        if (!isValidActionId(normalizedActionId)) {
            return serialize(recent);
        }
        recent.remove(normalizedActionId);
        recent.add(0, normalizedActionId);
        while (recent.size() > MAX_ITEMS) {
            recent.remove(recent.size() - 1);
        }
        return serialize(recent);
    }

    private static String normalize(String value) {
        return value == null ? "" : value.trim();
    }

    private static boolean isValidActionId(String value) {
        if (value.isEmpty() || value.length() > MAX_ACTION_ID_LENGTH) {
            return false;
        }
        for (int index = 0; index < value.length(); index++) {
            char character = value.charAt(index);
            if (!Character.isLetterOrDigit(character)
                    && character != '.'
                    && character != '_'
                    && character != '-') {
                return false;
            }
        }
        return true;
    }

    private static String serialize(List<String> actionIds) {
        StringBuilder serialized = new StringBuilder();
        for (String actionId : actionIds) {
            if (serialized.length() > 0) {
                serialized.append('\n');
            }
            serialized.append(actionId);
        }
        return serialized.toString();
    }
}
