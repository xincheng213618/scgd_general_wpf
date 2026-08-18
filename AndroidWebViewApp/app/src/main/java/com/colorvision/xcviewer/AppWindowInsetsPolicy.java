package com.colorvision.xcviewer;

final class AppWindowInsetsPolicy {
    private AppWindowInsetsPolicy() {
    }

    static int topContentInset(int statusBarTop, int displayCutoutTop) {
        return Math.max(0, Math.max(statusBarTop, displayCutoutTop));
    }
}
