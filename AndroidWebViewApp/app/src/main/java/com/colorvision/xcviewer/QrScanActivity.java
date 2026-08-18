package com.colorvision.xcviewer;

import android.Manifest;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.content.res.ColorStateList;
import android.graphics.Color;
import android.os.Bundle;
import android.view.Gravity;
import android.view.View;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.camera.core.Camera;
import androidx.camera.core.CameraSelector;
import androidx.camera.core.ImageAnalysis;
import androidx.camera.core.ImageProxy;
import androidx.camera.core.Preview;
import androidx.camera.core.TorchState;
import androidx.camera.lifecycle.ProcessCameraProvider;
import androidx.camera.view.PreviewView;
import androidx.core.content.ContextCompat;
import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowCompat;
import androidx.core.view.WindowInsetsCompat;
import androidx.core.view.WindowInsetsControllerCompat;
import androidx.appcompat.app.AppCompatActivity;

import com.google.android.material.button.MaterialButton;
import com.google.android.material.card.MaterialCardView;
import com.google.common.util.concurrent.ListenableFuture;

import java.nio.ByteBuffer;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicBoolean;

public class QrScanActivity extends AppCompatActivity {
    public static final String EXTRA_QR_RESULT = "qr_result";
    public static final String EXTRA_SCAN_FAILURE = "qr_scan_failure";

    private static final int REQUEST_CAMERA_PERMISSION = 2001;

    private final RuntimePermissionDialogState cameraPermissionDialogState =
            new RuntimePermissionDialogState();
    private final ExecutorService analysisExecutor = Executors.newSingleThreadExecutor(runnable -> {
        Thread thread = new Thread(runnable, "ColorVisionQrAnalysis");
        thread.setDaemon(true);
        return thread;
    });
    private final QrFrameDecoder frameDecoder = new QrFrameDecoder();
    private final AtomicBoolean completed = new AtomicBoolean();
    private final AtomicBoolean pairingHelpVisible = new AtomicBoolean();

    private FrameLayout root;
    private PreviewView previewView;
    private MaterialButton torchButton;
    private ProcessCameraProvider cameraProvider;
    private ImageAnalysis imageAnalysis;
    private Camera camera;
    private boolean cameraStartRequested;
    private boolean torchEnabled;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        configureSystemBars();
        createContentView();
        if (hasCameraPermission()) {
            startCameraIfReady();
        } else {
            requestCameraPermission();
        }
    }

    private void configureSystemBars() {
        WindowCompat.setDecorFitsSystemWindows(getWindow(), false);
        getWindow().setStatusBarColor(Color.BLACK);
        getWindow().setNavigationBarColor(Color.BLACK);
        WindowInsetsControllerCompat controller = WindowCompat.getInsetsController(
                getWindow(), getWindow().getDecorView());
        controller.setAppearanceLightStatusBars(false);
        controller.setAppearanceLightNavigationBars(false);
    }

    private void createContentView() {
        root = new FrameLayout(this);
        root.setBackgroundColor(Color.BLACK);

        previewView = new PreviewView(this);
        previewView.setImplementationMode(PreviewView.ImplementationMode.COMPATIBLE);
        previewView.setScaleType(PreviewView.ScaleType.FILL_CENTER);
        root.addView(previewView, new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT));

        MaterialButton backButton = createCameraIconButton(
                R.drawable.ic_arrow_back_24, getString(R.string.qr_scan_back));
        backButton.setOnClickListener(view -> finish());
        FrameLayout.LayoutParams backParams = new FrameLayout.LayoutParams(
                dp(56), dp(56), Gravity.TOP | Gravity.START);
        backParams.setMargins(dp(12), dp(12), 0, 0);
        root.addView(backButton, backParams);

        torchButton = createCameraIconButton(
                R.drawable.ic_flash_on_24, getString(R.string.qr_scan_torch_on));
        torchButton.setVisibility(View.GONE);
        torchButton.setOnClickListener(view -> toggleTorch());
        FrameLayout.LayoutParams torchParams = new FrameLayout.LayoutParams(
                dp(56), dp(56), Gravity.TOP | Gravity.END);
        torchParams.setMargins(0, dp(12), dp(12), 0);
        root.addView(torchButton, torchParams);

        MaterialCardView statusCard = new MaterialCardView(this);
        statusCard.setCardBackgroundColor(Color.argb(184, 0, 0, 0));
        statusCard.setCardElevation(0);
        statusCard.setRadius(dp(16));

        LinearLayout statusContent = new LinearLayout(this);
        statusContent.setOrientation(LinearLayout.VERTICAL);
        statusContent.setGravity(Gravity.CENTER_HORIZONTAL);
        statusContent.setPadding(dp(10), dp(6), dp(10), dp(6));

        TextView statusText = new TextView(this);
        statusText.setText(R.string.qr_scan_prompt);
        statusText.setTextAppearance(com.google.android.material.R.style.TextAppearance_Material3_BodyLarge);
        statusText.setTextColor(Color.WHITE);
        statusText.setGravity(Gravity.CENTER);
        statusText.setPadding(dp(8), dp(8), dp(8), 0);
        statusContent.addView(statusText, new LinearLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.WRAP_CONTENT));

        MaterialButton helpButton = new MaterialButton(
                this, null, com.google.android.material.R.attr.materialButtonOutlinedStyle);
        helpButton.setText(R.string.qr_scan_help);
        helpButton.setTextColor(Color.WHITE);
        helpButton.setStrokeColor(ColorStateList.valueOf(Color.WHITE));
        helpButton.setStrokeWidth(dp(1));
        helpButton.setMinimumHeight(dp(48));
        helpButton.setInsetTop(0);
        helpButton.setInsetBottom(0);
        helpButton.setOnClickListener(view -> showPairingHelp());
        statusContent.addView(helpButton, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                dp(48)));

        statusCard.addView(statusContent, new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.WRAP_CONTENT));
        FrameLayout.LayoutParams statusParams = new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.WRAP_CONTENT,
                Gravity.BOTTOM);
        statusParams.setMargins(dp(18), 0, dp(18), dp(16));
        root.addView(statusCard, statusParams);

        ViewCompat.setOnApplyWindowInsetsListener(root, (view, windowInsets) -> {
            Insets statusBars = windowInsets.getInsets(WindowInsetsCompat.Type.statusBars());
            Insets displayCutout = windowInsets.getInsets(WindowInsetsCompat.Type.displayCutout());
            Insets navigationBars = windowInsets.getInsets(WindowInsetsCompat.Type.navigationBars());
            backParams.topMargin = AppWindowInsetsPolicy.topContentInset(
                    statusBars.top, displayCutout.top) + dp(12);
            torchParams.topMargin = backParams.topMargin;
            statusParams.bottomMargin = Math.max(
                    navigationBars.bottom, displayCutout.bottom) + dp(16);
            backButton.setLayoutParams(backParams);
            torchButton.setLayoutParams(torchParams);
            statusCard.setLayoutParams(statusParams);
            return windowInsets;
        });
        setContentView(root);
        ViewCompat.requestApplyInsets(root);
    }

    private MaterialButton createCameraIconButton(int iconResource, String description) {
        MaterialButton button = new MaterialButton(
                this, null, com.google.android.material.R.attr.materialIconButtonStyle);
        button.setIconResource(iconResource);
        button.setIconTint(ColorStateList.valueOf(Color.WHITE));
        button.setIconSize(dp(24));
        button.setIconPadding(0);
        button.setGravity(Gravity.CENTER);
        button.setPadding(0, 0, 0, 0);
        button.setInsetTop(0);
        button.setInsetBottom(0);
        button.setCornerRadius(dp(28));
        button.setBackgroundTintList(ColorStateList.valueOf(Color.argb(128, 0, 0, 0)));
        button.setContentDescription(description);
        return button;
    }

    private boolean hasCameraPermission() {
        return checkSelfPermission(Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED;
    }

    private void requestCameraPermission() {
        int requestGeneration = cameraPermissionDialogState.begin();
        requestPermissions(new String[]{Manifest.permission.CAMERA}, REQUEST_CAMERA_PERMISSION);
        root.postDelayed(() -> cameraPermissionDialogState.observe(
                        requestGeneration, hasCameraPermission(), hasWindowFocus()),
                RuntimePermissionDialogState.OBSERVE_DELAY_MILLISECONDS);
        root.postDelayed(() -> recoverBlockedCameraPermissionRequest(requestGeneration),
                RuntimePermissionDialogState.NO_DIALOG_RECOVERY_DELAY_MILLISECONDS);
    }

    private void recoverBlockedCameraPermissionRequest(int requestGeneration) {
        if (completed.get() || !cameraPermissionDialogState.shouldRecoverAsBlocked(
                requestGeneration, hasCameraPermission(), hasWindowFocus())) {
            return;
        }
        finishWithFailure(QrScanFailurePresentation.CAMERA_PERMISSION_BLOCKED);
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, String[] permissions, int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != REQUEST_CAMERA_PERMISSION) {
            return;
        }

        boolean granted = grantResults.length > 0
                && grantResults[0] == PackageManager.PERMISSION_GRANTED;
        if (granted) {
            cameraPermissionDialogState.completeFromSystemResult(true);
            startCameraIfReady();
            return;
        }
        if (cameraPermissionDialogState.completeFromSystemResult(false)) {
            finishWithFailure(QrScanFailurePresentation.CAMERA_PERMISSION_DENIED);
        }
    }

    private void startCameraIfReady() {
        if (!hasCameraPermission() || cameraStartRequested || completed.get()) {
            return;
        }
        cameraStartRequested = true;
        ListenableFuture<ProcessCameraProvider> providerFuture =
                ProcessCameraProvider.getInstance(this);
        providerFuture.addListener(() -> bindCamera(providerFuture),
                ContextCompat.getMainExecutor(this));
    }

    private void bindCamera(ListenableFuture<ProcessCameraProvider> providerFuture) {
        if (completed.get() || isFinishing() || isDestroyed()) {
            return;
        }
        try {
            cameraProvider = providerFuture.get();
            Preview preview = new Preview.Builder().build();
            preview.setSurfaceProvider(previewView.getSurfaceProvider());

            imageAnalysis = new ImageAnalysis.Builder()
                    .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
                    .build();
            imageAnalysis.setAnalyzer(analysisExecutor, this::analyzeFrame);

            cameraProvider.unbindAll();
            camera = cameraProvider.bindToLifecycle(
                    this,
                    CameraSelector.DEFAULT_BACK_CAMERA,
                    preview,
                    imageAnalysis);
            boolean hasFlash = camera.getCameraInfo().hasFlashUnit();
            torchButton.setVisibility(hasFlash ? View.VISIBLE : View.GONE);
            camera.getCameraInfo().getTorchState().observe(this, state -> {
                torchEnabled = state != null && state == TorchState.ON;
                updateTorchButton();
            });
        } catch (Exception ex) {
            cameraStartRequested = false;
            finishWithFailure(QrScanFailurePresentation.CAMERA_UNAVAILABLE);
        }
    }

    private void analyzeFrame(ImageProxy image) {
        try {
            if (!QrScanFramePolicy.shouldAnalyze(
                    completed.get(), pairingHelpVisible.get(), image.getPlanes().length)) {
                return;
            }
            ImageProxy.PlaneProxy lumaPlane = image.getPlanes()[0];
            ByteBuffer buffer = lumaPlane.getBuffer();
            byte[] luma = QrFrameDecoder.copyLumaPlane(
                    buffer,
                    image.getWidth(),
                    image.getHeight(),
                    lumaPlane.getRowStride(),
                    lumaPlane.getPixelStride());
            String text = frameDecoder.decode(
                    luma,
                    image.getWidth(),
                    image.getHeight(),
                    image.getImageInfo().getRotationDegrees());
            if (!pairingHelpVisible.get() && text != null && !text.isEmpty()) {
                finishWithQrResult(text);
            }
        } catch (RuntimeException ignored) {
            // A malformed vendor frame is dropped; CameraX supplies the next latest frame.
        } finally {
            image.close();
        }
    }

    private void finishWithQrResult(String text) {
        if (!completed.compareAndSet(false, true)) {
            return;
        }
        ContextCompat.getMainExecutor(this).execute(() -> {
            if (isFinishing() || isDestroyed()) {
                return;
            }
            Intent result = new Intent();
            result.putExtra(EXTRA_QR_RESULT, text);
            setResult(RESULT_OK, result);
            finish();
        });
    }

    private void finishWithFailure(String reason) {
        if (!completed.compareAndSet(false, true)) {
            return;
        }
        Intent result = new Intent();
        result.putExtra(EXTRA_SCAN_FAILURE, reason);
        setResult(RESULT_CANCELED, result);
        finish();
    }

    private void toggleTorch() {
        if (camera == null || !camera.getCameraInfo().hasFlashUnit()) {
            return;
        }
        torchButton.setEnabled(false);
        ListenableFuture<Void> request = camera.getCameraControl().enableTorch(!torchEnabled);
        request.addListener(() -> {
            if (!isDestroyed()) {
                torchButton.setEnabled(true);
            }
        }, ContextCompat.getMainExecutor(this));
    }

    private void showPairingHelp() {
        if (!pairingHelpVisible.compareAndSet(false, true)) {
            return;
        }
        PairingHelpDialog.showDuringScan(
                this, () -> pairingHelpVisible.set(false));
    }

    private void updateTorchButton() {
        if (torchButton == null) {
            return;
        }
        torchButton.setIconTint(ColorStateList.valueOf(torchEnabled ? Color.BLACK : Color.WHITE));
        torchButton.setBackgroundTintList(ColorStateList.valueOf(torchEnabled
                ? Color.WHITE : Color.argb(128, 0, 0, 0)));
        torchButton.setContentDescription(getString(torchEnabled
                ? R.string.qr_scan_torch_off : R.string.qr_scan_torch_on));
    }

    @Override
    protected void onDestroy() {
        completed.set(true);
        if (imageAnalysis != null) {
            imageAnalysis.clearAnalyzer();
        }
        if (cameraProvider != null) {
            cameraProvider.unbindAll();
        }
        analysisExecutor.shutdownNow();
        super.onDestroy();
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
