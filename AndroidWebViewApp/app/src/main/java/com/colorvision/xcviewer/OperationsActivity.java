package com.colorvision.xcviewer;

import android.app.Activity;
import android.app.AlertDialog;
import android.app.KeyguardManager;
import android.content.Context;
import android.content.Intent;
import android.graphics.Color;
import android.os.Build;
import android.os.Bundle;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import org.json.JSONObject;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public class OperationsActivity extends Activity {
    public static final String EXTRA_PAIRING_PAYLOAD = "operations_pairing_payload";
    private static final int REQUEST_APPROVAL_CREDENTIAL = 3101;

    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private AppPreferences preferences;
    private OperationsApiClient client;
    private TextView title;
    private TextView state;
    private TextView details;
    private ProgressBar progress;
    private LinearLayout actions;
    private String pendingApprovalJobId = "";

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        preferences = new AppPreferences(this);
        createView();

        String rawPairing = getIntent().getStringExtra(EXTRA_PAIRING_PAYLOAD);
        if (rawPairing != null && !rawPairing.isEmpty()) {
            beginPairing(rawPairing);
        } else if (preferences.hasOperationsProfile()) {
            openExistingProfile();
        } else {
            showError("尚未安全配对", "请返回并扫描电脑端的现场运维配对码。", null);
        }
    }

    private void createView() {
        ScrollView scroll = new ScrollView(this);
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(dp(22), dp(20), dp(22), dp(28));
        root.setBackgroundColor(Color.rgb(245, 247, 250));
        scroll.addView(root, new ScrollView.LayoutParams(
                ScrollView.LayoutParams.MATCH_PARENT, ScrollView.LayoutParams.WRAP_CONTENT));

        Button back = new Button(this);
        back.setText("返回");
        back.setOnClickListener(v -> finish());
        root.addView(back, new LinearLayout.LayoutParams(dp(88), dp(44)));

        title = new TextView(this);
        title.setText("ColorVision 现场运维");
        title.setTextSize(25);
        title.setTextColor(Color.rgb(24, 35, 49));
        title.setPadding(0, dp(20), 0, dp(6));
        root.addView(title);

        state = new TextView(this);
        state.setTextSize(15);
        state.setTextColor(Color.rgb(58, 75, 92));
        root.addView(state);

        progress = new ProgressBar(this);
        LinearLayout.LayoutParams progressParams = new LinearLayout.LayoutParams(dp(44), dp(44));
        progressParams.gravity = Gravity.CENTER_HORIZONTAL;
        progressParams.setMargins(0, dp(24), 0, dp(16));
        root.addView(progress, progressParams);

        details = new TextView(this);
        details.setTextSize(14);
        details.setTextColor(Color.rgb(41, 53, 66));
        details.setPadding(dp(16), dp(16), dp(16), dp(16));
        details.setBackgroundColor(Color.WHITE);
        details.setTextIsSelectable(true);
        root.addView(details, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT));

        actions = new LinearLayout(this);
        actions.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams actionsParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        actionsParams.setMargins(0, dp(18), 0, 0);
        root.addView(actions, actionsParams);

        setContentView(scroll);
    }

    private void beginPairing(String rawPairing) {
        setBusy("正在验证配对码并创建设备身份…");
        executor.execute(() -> {
            try {
                OperationsPairingPayload payload = OperationsPairingPayload.parse(rawPairing);
                String deviceId = preferences.getOrCreateDeviceId();
                String deviceName = Build.MANUFACTURER + " " + Build.MODEL;
                OperationsDeviceIdentity identity = new OperationsDeviceIdentity(payload.hostId);
                OperationsApiClient pairingClient = new OperationsApiClient(
                        payload.endpoint, payload.certificateSha256, deviceId, identity);
                pairingClient.submitClaim(payload, deviceName.trim());
                runOnUiThread(() -> {
                    state.setText("已提交安全证明，请在电脑端批准这台设备");
                    details.setText("设备：" + deviceName + "\n权限：状态、告警、诊断摘要（只读）\n配对码：一次性，短时有效");
                });

                for (int attempt = 0; attempt < 60; attempt++) {
                    if (isFinishing()) {
                        return;
                    }
                    JSONObject response = pairingClient.pairingStatus(payload.pairingId);
                    JSONObject data = response.optJSONObject("data");
                    String status = data == null ? "" : data.optString("status", "");
                    if ("approved".equals(status)) {
                        preferences.saveOperationsProfile(payload.endpoint, payload.certificateSha256, payload.hostId);
                        client = pairingClient;
                        runOnUiThread(this::showDashboard);
                        return;
                    }
                    if ("rejected".equals(status)) {
                        runOnUiThread(() -> showError("配对被拒绝", "电脑端拒绝了这台设备。", null));
                        return;
                    }
                    Thread.sleep(2000);
                }
                runOnUiThread(() -> showError("等待批准超时", "请在电脑端刷新配对码后重试。", null));
            } catch (Exception ex) {
                runOnUiThread(() -> showError("安全配对失败", readableError(ex), null));
            }
        });
    }

    private void openExistingProfile() {
        setBusy("正在连接已配对的 ColorVision 主机…");
        executor.execute(() -> {
            try {
                OperationsDeviceIdentity identity = new OperationsDeviceIdentity(preferences.getOperationsHostId());
                client = new OperationsApiClient(
                        preferences.getOperationsEndpoint(),
                        preferences.getOperationsCertificatePin(),
                        preferences.getOrCreateDeviceId(),
                        identity);
                client.get("/ops/v1/snapshot");
                runOnUiThread(this::showDashboard);
            } catch (Exception ex) {
                runOnUiThread(() -> showError("无法连接安全运维通道", readableError(ex), this::clearProfile));
            }
        });
    }

    private void showDashboard() {
        progress.setVisibility(View.GONE);
        title.setText("现场运维概览");
        state.setText("已通过设备密钥安全连接\n" + preferences.getOperationsEndpoint());
        details.setText("选择下方只读能力查看现场信息。写操作和特权命令不会由此页面直接执行。");
        actions.removeAllViews();
        addAction("刷新运行状态", "/ops/v1/snapshot");
        addAction("查看当前告警", "/ops/v1/alerts");
        addAction("查看诊断摘要", "/ops/v1/diagnostics/summary");
        addAction("查看能力目录", "/ops/v1/capabilities");
        addAction("查看支持消息", "/ops/v1/support-messages");

        Button jobs = new Button(this);
        jobs.setText("作业与审批");
        jobs.setOnClickListener(v -> showJobs());
        actions.addView(jobs, actionParams());

        Button diagnostic = new Button(this);
        diagnostic.setText("申请诊断包");
        diagnostic.setOnClickListener(v -> confirmCreateDiagnosticJob());
        actions.addView(diagnostic, actionParams());

        Button receipt = new Button(this);
        receipt.setText("提交部署确认");
        receipt.setOnClickListener(v -> confirmDeploymentReceipt());
        actions.addView(receipt, actionParams());

        Button support = new Button(this);
        support.setText("申请限时远程支持");
        support.setOnClickListener(v -> confirmSupportRequest());
        actions.addView(support, actionParams());

        Button disconnect = new Button(this);
        disconnect.setText("撤下本机配对资料");
        disconnect.setOnClickListener(v -> clearProfile());
        actions.addView(disconnect, actionParams());
        loadCapability("/ops/v1/snapshot");
    }

    private void showJobs() {
        progress.setVisibility(View.VISIBLE);
        executor.execute(() -> {
            try {
                JSONObject response = client.get("/ops/v1/jobs");
                JSONObject data = response.optJSONObject("data");
                org.json.JSONArray jobs = data == null ? null : data.optJSONArray("jobs");
                JSONObject waiting = null;
                if (jobs != null) {
                    for (int index = 0; index < jobs.length(); index++) {
                        JSONObject job = jobs.optJSONObject(index);
                        if (job != null && "awaiting_mobile_approval".equals(job.optString("status"))) {
                            waiting = job;
                            break;
                        }
                    }
                }
                JSONObject finalWaiting = waiting;
                runOnUiThread(() -> {
                    progress.setVisibility(View.GONE);
                    state.setText(finalWaiting == null ? "当前没有待移动审批作业" : "发现待审批作业");
                    details.setText(pretty(data == null ? response : data));
                    if (finalWaiting != null) {
                        addApprovalActions(finalWaiting);
                    }
                });
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void addApprovalActions(JSONObject job) {
        String jobId = job.optString("jobId", "");
        Button approve = new Button(this);
        approve.setText("验证设备凭据并批准此作业");
        approve.setOnClickListener(v -> requestCredentialForApproval(jobId));
        actions.addView(approve, 0, actionParams());
        Button reject = new Button(this);
        reject.setText("拒绝此作业");
        reject.setOnClickListener(v -> decideJob(jobId, false));
        actions.addView(reject, 1, actionParams());
    }

    private void requestCredentialForApproval(String jobId) {
        KeyguardManager keyguard = (KeyguardManager) getSystemService(Context.KEYGUARD_SERVICE);
        if (keyguard == null || !keyguard.isDeviceSecure()) {
            Toast.makeText(this, "批准作业前必须先为手机设置锁屏凭据", Toast.LENGTH_LONG).show();
            return;
        }
        Intent credential = keyguard.createConfirmDeviceCredentialIntent("批准现场运维作业", "验证后仍需电脑端本机共签");
        if (credential == null) {
            Toast.makeText(this, "无法启动设备凭据验证", Toast.LENGTH_LONG).show();
            return;
        }
        pendingApprovalJobId = jobId;
        startActivityForResult(credential, REQUEST_APPROVAL_CREDENTIAL);
    }

    private void decideJob(String jobId, boolean approved) {
        progress.setVisibility(View.VISIBLE);
        executor.execute(() -> {
            try {
                JSONObject body = new JSONObject();
                body.put("approved", approved);
                body.put("reason", approved ? "移动端设备凭据已验证" : "现场运维人员拒绝");
                JSONObject response = client.post("/ops/v1/jobs/" + jobId + "/decision", body);
                runOnUiThread(() -> {
                    progress.setVisibility(View.GONE);
                    state.setText(approved ? "移动审批已记录，等待电脑端本机共签" : "作业已拒绝");
                    details.setText(pretty(response));
                });
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void confirmCreateDiagnosticJob() {
        new AlertDialog.Builder(this)
                .setTitle("申请诊断包")
                .setMessage("诊断包只包含有界的运行信息和脱敏审计，不含凭据、用户文档、数据库或图像。提交后仍需电脑端本机确认。")
                .setNegativeButton("取消", null)
                .setPositiveButton("提交申请", (dialog, which) -> createDiagnosticJob())
                .show();
    }

    private void createDiagnosticJob() {
        executor.execute(() -> {
            try {
                JSONObject body = new JSONObject();
                body.put("capabilityId", "ops.diagnostics.bundle.create");
                body.put("reason", "现场支持诊断");
                body.put("input", new JSONObject());
                client.post("/ops/v1/jobs", body);
                runOnUiThread(() -> {
                    state.setText("诊断作业已创建，请进入“作业与审批”完成移动审批");
                    showJobs();
                });
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void confirmDeploymentReceipt() {
        new AlertDialog.Builder(this)
                .setTitle("提交部署确认")
                .setMessage("仅提交本移动伴侣当前版本的验证收据，不会触发远程部署。")
                .setNegativeButton("取消", null)
                .setPositiveButton("确认", (dialog, which) -> submitDeploymentReceipt())
                .show();
    }

    private void submitDeploymentReceipt() {
        executor.execute(() -> {
            try {
                String version = getPackageManager().getPackageInfo(getPackageName(), 0).versionName;
                JSONObject body = new JSONObject();
                body.put("releaseId", "android-companion-" + version);
                body.put("version", version);
                body.put("status", "verified");
                body.put("evidenceSha256", "");
                JSONObject response = client.post("/ops/v1/deployment-receipts", body);
                runOnUiThread(() -> {
                    state.setText("部署确认已记录");
                    details.setText(pretty(response));
                });
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void confirmSupportRequest() {
        new AlertDialog.Builder(this)
                .setTitle("申请限时支持")
                .setMessage("仅申请 15 分钟诊断支持。电脑端必须本机允许；不开放任意远程桌面或命令。")
                .setNegativeButton("取消", null)
                .setPositiveButton("提交申请", (dialog, which) -> submitSupportRequest())
                .show();
    }

    private void submitSupportRequest() {
        executor.execute(() -> {
            try {
                JSONObject body = new JSONObject();
                body.put("mode", "diagnostics");
                body.put("reason", "现场设备请求诊断支持");
                body.put("durationMinutes", 15);
                JSONObject response = client.post("/ops/v1/support-sessions", body);
                runOnUiThread(() -> {
                    state.setText("支持请求已提交，等待电脑端本机同意");
                    details.setText(pretty(response));
                });
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void showTransientError(Exception ex) {
        progress.setVisibility(View.GONE);
        state.setText("操作失败");
        details.setText(readableError(ex));
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode == REQUEST_APPROVAL_CREDENTIAL) {
            if (resultCode == RESULT_OK && !pendingApprovalJobId.isEmpty()) {
                String jobId = pendingApprovalJobId;
                pendingApprovalJobId = "";
                decideJob(jobId, true);
            } else {
                pendingApprovalJobId = "";
                Toast.makeText(this, "未完成设备凭据验证，未批准作业", Toast.LENGTH_LONG).show();
            }
            return;
        }
        super.onActivityResult(requestCode, resultCode, data);
    }

    private void addAction(String label, String path) {
        Button button = new Button(this);
        button.setText(label);
        button.setOnClickListener(v -> loadCapability(path));
        actions.addView(button, actionParams());
    }

    private void loadCapability(String path) {
        progress.setVisibility(View.VISIBLE);
        state.setText("正在读取…");
        executor.execute(() -> {
            try {
                JSONObject response = client.get(path);
                runOnUiThread(() -> {
                    progress.setVisibility(View.GONE);
                    state.setText("读取成功 · " + path);
                    JSONObject data = response.optJSONObject("data");
                    details.setText(pretty(data == null ? response : data));
                });
            } catch (Exception ex) {
                runOnUiThread(() -> {
                    progress.setVisibility(View.GONE);
                    state.setText("读取失败");
                    details.setText(readableError(ex));
                });
            }
        });
    }

    private void clearProfile() {
        try {
            String hostId = preferences.getOperationsHostId();
            if (!hostId.isEmpty()) {
                new OperationsDeviceIdentity(hostId).delete();
            }
        } catch (Exception ignored) {
        }
        preferences.clearOperationsProfile();
        Toast.makeText(this, "本机配对资料已移除；电脑端仍可单独撤销设备", Toast.LENGTH_LONG).show();
        finish();
    }

    private void setBusy(String message) {
        state.setText(message);
        details.setText("设备私钥只保存在 Android Keystore，不会写入二维码、网址或应用配置。 ");
        progress.setVisibility(View.VISIBLE);
        actions.removeAllViews();
    }

    private void showError(String heading, String message, Runnable recovery) {
        progress.setVisibility(View.GONE);
        title.setText(heading);
        state.setText(message);
        details.setText("请确认手机与电脑位于同一可信局域网，并重新扫描电脑端短时配对码。\n不会回退到 URL token。 ");
        actions.removeAllViews();
        if (recovery != null) {
            Button button = new Button(this);
            button.setText("移除失效配对资料");
            button.setOnClickListener(v -> recovery.run());
            actions.addView(button, actionParams());
        }
    }

    private LinearLayout.LayoutParams actionParams() {
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, dp(50));
        params.setMargins(0, 0, 0, dp(10));
        return params;
    }

    private String readableError(Exception ex) {
        String message = ex.getMessage();
        if (message == null || message.trim().isEmpty()) {
            return ex.getClass().getSimpleName();
        }
        if (message.contains("Certificate pin mismatch")) {
            return "服务器证书与二维码指纹不一致，已阻止连接。";
        }
        if (message.contains("unknown_or_revoked_device")) {
            return "设备已被电脑端撤销，请重新配对。";
        }
        return message;
    }

    private String pretty(JSONObject value) {
        try {
            return value.toString(2);
        } catch (Exception ignored) {
            return value.toString();
        }
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }

    @Override
    protected void onDestroy() {
        executor.shutdownNow();
        super.onDestroy();
    }
}
