(function () {
    function escapeHtml(value) {
        return String(value)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/\"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function clamp(value, min, max) {
        return Math.min(max, Math.max(min, value));
    }

    function isEditableTarget(target) {
        return Boolean(target && target.closest("input, textarea, select, button, a"));
    }

    function buildTooltipHtml(data) {
        const highlights = Array.isArray(data.changelogHighlights) ? data.changelogHighlights : [];
        const meta = [
            `${data.fileCount || 0} 个历史文件`,
            `${data.fixCount || 0} 个 fix`,
            data.latestVersion ? `最新 ${data.latestVersion}` : "",
            data.changelogDate ? `CHANGELOG ${data.changelogDate}` : "",
        ].filter(Boolean);
        const header = `<strong>${escapeHtml(data.branch || "未命名版本")}</strong>`;
        const summary = `<div>${meta.map(escapeHtml).join(" · ")}</div>`;
        const badge = data.annotationLabel ? `<div class="mt-1"><span>${escapeHtml(data.annotationLabel)}</span></div>` : "";
        const reason = data.annotationReason ? `<div class="mt-1">${escapeHtml(data.annotationReason)}</div>` : "";
        const kind = data.kindSummary ? `<div class="mt-1 text-white-50">${escapeHtml(data.kindSummary)}</div>` : "";
        const list = highlights.length
            ? `<ul>${highlights.map((item) => `<li>${escapeHtml(item)}</li>`).join("")}</ul>`
            : "";
        return `${header}${summary}${badge}${reason}${kind}${list}`;
    }

    function initTimeline(container) {
        if (!container || container.dataset.timelineInitialized === "true") {
            return;
        }

        const viewport = container.querySelector(".release-timeline-viewport");
        const stage = container.querySelector(".release-timeline-stage");
        const tooltip = container.querySelector(".release-timeline-tooltip");
        const scaleText = container.querySelector(".js-release-timeline-scale");
        if (!viewport || !stage || !tooltip) {
            return;
        }

        container.dataset.timelineInitialized = "true";

        const baseWidth = Number(stage.dataset.baseWidth || stage.scrollWidth || 900);
        const minScale = Number(container.dataset.scaleMin || 0.6);
        const maxScale = Number(container.dataset.scaleMax || 4);
        let scale = clamp(Number(container.dataset.initialScale || 1), minScale, maxScale);
        let dragState = null;
        let activeTooltipNode = null;

        function setTooltipHidden(hidden) {
            tooltip.hidden = hidden;
            tooltip.setAttribute("aria-hidden", hidden ? "true" : "false");
        }

        function stageWidth() {
            return stage.getBoundingClientRect().width || baseWidth;
        }

        function applyScale(anchorRatio) {
            const previousWidth = stageWidth();
            stage.style.width = `${baseWidth * scale}px`;
            if (scaleText) {
                scaleText.textContent = `${Math.round(scale * 100)}%`;
            }
            const nextWidth = stageWidth();
            if (typeof anchorRatio === "number") {
                viewport.scrollLeft = Math.max(0, nextWidth * anchorRatio - viewport.clientWidth / 2);
            } else if (previousWidth > 0 && nextWidth > 0) {
                const ratio = viewport.scrollLeft / previousWidth;
                viewport.scrollLeft = Math.max(0, ratio * nextWidth);
            }
        }

        function positionTooltip(clientX, clientY) {
            const shellRect = container.getBoundingClientRect();
            const tooltipRect = tooltip.getBoundingClientRect();
            const left = clamp(clientX - shellRect.left + 18, 12, Math.max(12, shellRect.width - tooltipRect.width - 12));
            const top = clamp(clientY - shellRect.top + 18, 12, Math.max(12, shellRect.height - tooltipRect.height - 12));
            tooltip.style.left = `${left}px`;
            tooltip.style.top = `${top}px`;
        }

        function showTooltip(target, clientX, clientY) {
            try {
                const data = JSON.parse(target.dataset.tooltip || "{}");
                tooltip.innerHTML = buildTooltipHtml(data);
                activeTooltipNode = target;
                setTooltipHidden(false);
                positionTooltip(clientX, clientY);
            } catch (_error) {
                setTooltipHidden(true);
            }
        }

        function hideTooltip() {
            activeTooltipNode = null;
            setTooltipHidden(true);
        }

        function refreshTooltipPosition() {
            if (!activeTooltipNode || tooltip.hidden) {
                return;
            }
            const rect = activeTooltipNode.getBoundingClientRect();
            positionTooltip(rect.left + rect.width / 2, rect.top + rect.height / 2);
        }

        container.querySelectorAll(".release-timeline-node").forEach((node) => {
            node.addEventListener("mouseenter", (event) => showTooltip(node, event.clientX, event.clientY));
            node.addEventListener("mousemove", (event) => showTooltip(node, event.clientX, event.clientY));
            node.addEventListener("mouseleave", hideTooltip);
            node.addEventListener("focus", () => {
                const rect = node.getBoundingClientRect();
                showTooltip(node, rect.left + rect.width / 2, rect.top + rect.height / 2);
            });
            node.addEventListener("blur", hideTooltip);
        });

        container.querySelectorAll("[data-action='zoom-in']").forEach((button) => {
            button.addEventListener("click", () => {
                scale = clamp(scale + 0.2, minScale, maxScale);
                applyScale((viewport.scrollLeft + viewport.clientWidth / 2) / stageWidth());
                refreshTooltipPosition();
            });
        });
        container.querySelectorAll("[data-action='zoom-out']").forEach((button) => {
            button.addEventListener("click", () => {
                scale = clamp(scale - 0.2, minScale, maxScale);
                applyScale((viewport.scrollLeft + viewport.clientWidth / 2) / stageWidth());
                refreshTooltipPosition();
            });
        });
        container.querySelectorAll("[data-action='reset']").forEach((button) => {
            button.addEventListener("click", () => {
                scale = 1;
                applyScale(0.5);
                refreshTooltipPosition();
            });
        });

        viewport.addEventListener(
            "wheel",
            (event) => {
                if (event.ctrlKey || event.metaKey) {
                    event.preventDefault();
                    const rect = viewport.getBoundingClientRect();
                    const anchorRatio = clamp((viewport.scrollLeft + (event.clientX - rect.left)) / stageWidth(), 0, 1);
                    scale = clamp(scale + (event.deltaY < 0 ? 0.15 : -0.15), minScale, maxScale);
                    applyScale(anchorRatio);
                    refreshTooltipPosition();
                    return;
                }
                if (Math.abs(event.deltaY) > Math.abs(event.deltaX)) {
                    event.preventDefault();
                    viewport.scrollLeft += event.deltaY;
                    refreshTooltipPosition();
                }
            },
            { passive: false }
        );

        viewport.addEventListener("scroll", refreshTooltipPosition, { passive: true });
        window.addEventListener("resize", refreshTooltipPosition, { passive: true });

        viewport.addEventListener("keydown", (event) => {
            if (isEditableTarget(event.target)) {
                return;
            }

            if (event.key === "ArrowLeft") {
                event.preventDefault();
                viewport.scrollLeft -= 96;
                refreshTooltipPosition();
                return;
            }
            if (event.key === "ArrowRight") {
                event.preventDefault();
                viewport.scrollLeft += 96;
                refreshTooltipPosition();
                return;
            }
            if (event.key === "Home") {
                event.preventDefault();
                viewport.scrollLeft = 0;
                refreshTooltipPosition();
                return;
            }
            if (event.key === "End") {
                event.preventDefault();
                viewport.scrollLeft = viewport.scrollWidth;
                refreshTooltipPosition();
                return;
            }
            if (event.key === "+" || event.key === "=") {
                event.preventDefault();
                scale = clamp(scale + 0.2, minScale, maxScale);
                applyScale((viewport.scrollLeft + viewport.clientWidth / 2) / stageWidth());
                refreshTooltipPosition();
                return;
            }
            if (event.key === "-" || event.key === "_") {
                event.preventDefault();
                scale = clamp(scale - 0.2, minScale, maxScale);
                applyScale((viewport.scrollLeft + viewport.clientWidth / 2) / stageWidth());
                refreshTooltipPosition();
                return;
            }
            if (event.key === "0") {
                event.preventDefault();
                scale = 1;
                applyScale(0.5);
                refreshTooltipPosition();
            }
        });

        viewport.addEventListener("pointerdown", (event) => {
            if (event.button !== 0 || isEditableTarget(event.target)) {
                return;
            }
            dragState = {
                pointerId: event.pointerId,
                startX: event.clientX,
                scrollLeft: viewport.scrollLeft,
            };
            viewport.classList.add("is-dragging");
            viewport.setPointerCapture(event.pointerId);
        });
        viewport.addEventListener("pointermove", (event) => {
            if (!dragState || event.pointerId !== dragState.pointerId) {
                return;
            }
            viewport.scrollLeft = dragState.scrollLeft - (event.clientX - dragState.startX);
            refreshTooltipPosition();
        });
        function endDrag(event) {
            if (!dragState || event.pointerId !== dragState.pointerId) {
                return;
            }
            viewport.classList.remove("is-dragging");
            if (viewport.hasPointerCapture(event.pointerId)) {
                viewport.releasePointerCapture(event.pointerId);
            }
            dragState = null;
        }
        viewport.addEventListener("pointerup", endDrag);
        viewport.addEventListener("pointercancel", endDrag);
        viewport.addEventListener("lostpointercapture", () => {
            viewport.classList.remove("is-dragging");
            dragState = null;
        });

        applyScale(0.5);
    }

    function boot() {
        document.querySelectorAll(".js-release-timeline").forEach(initTimeline);
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", boot);
    } else {
        boot();
    }
})();


