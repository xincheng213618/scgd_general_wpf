package com.colorvision.xcviewer;

import com.google.android.material.badge.BadgeDrawable;
import com.google.android.material.navigation.NavigationBarView;

final class OperationsProblemBadgeRenderer {
    private OperationsProblemBadgeRenderer() {
    }

    static void render(
            NavigationBarView navigation,
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
        if (model.number > 0) {
            badge.setMaxCharacterCount(3);
            badge.setNumber(model.number);
            badge.setContentDescriptionQuantityStringsResource(
                    R.plurals.operations_problem_badge_count);
        } else {
            badge.clearNumber();
            badge.setContentDescriptionNumberless(model.contentDescription);
        }
        badge.setVisible(true);
    }
}
