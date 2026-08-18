package com.colorvision.xcviewer;

import com.google.android.material.badge.BadgeDrawable;
import com.google.android.material.bottomnavigation.BottomNavigationView;

final class OperationsProblemBadgeRenderer {
    private OperationsProblemBadgeRenderer() {
    }

    static void render(
            BottomNavigationView navigation,
            int itemId,
            OperationsProblemBadgePresentation.ViewModel model) {
        if (navigation == null) {
            return;
        }
        if (!model.visible) {
            navigation.removeBadge(itemId);
            return;
        }
        BadgeDrawable badge = navigation.getOrCreateBadge(itemId);
        badge.clearNumber();
        badge.setContentDescriptionNumberless(model.contentDescription);
        badge.setVisible(true);
    }
}
