/* UniShare - availability calendar modal + toast.
   Reads item and owner schedule from #avail-modal. Slot-level reservations are shown on the request form. */
(function () {
    "use strict";

    var modal = document.getElementById("avail-modal");
    if (!modal) return;

    function $(id) { return document.getElementById(id); }

    var cfg = modal.dataset;

    var dayNumbers = { Sun: 0, Mon: 1, Tue: 2, Wed: 3, Thu: 4, Fri: 5, Sat: 6 };
    var availableDays = (cfg.availableDays || "").split(",").map(function (d) { return dayNumbers[d]; }).filter(function (d) { return d !== undefined; });
    var MAX_MONTHS_AHEAD = 3;
    var today = new Date(); today.setHours(0, 0, 0, 0);
    var cal = { y: 0, m: 0, sel: null };
    var toastTimer;

    function pad(n) { return n < 10 ? "0" + n : "" + n; }
    function dkey(d) { return d.getFullYear() + "-" + pad(d.getMonth() + 1) + "-" + pad(d.getDate()); }
    function parseKey(k) { var p = k.split("-"); return new Date(+p[0], +p[1] - 1, +p[2]); }
    function fmtShort(k) { return parseKey(k).toLocaleDateString("en-US", { month: "short", day: "numeric" }); }

    /* ---- toast ---- */
    function toast(msg) {
        var t = $("ua-toast");
        if (!t) return;
        t.textContent = msg;
        t.classList.add("show");
        clearTimeout(toastTimer);
        toastTimer = setTimeout(function () { t.classList.remove("show"); }, 3000);
    }

    /* ---- modal ---- */
    function showModal() {
        modal.classList.add("open");
        modal.setAttribute("aria-hidden", "false");
        document.body.classList.add("ua-lock");
    }

    function hideModal() {
        modal.classList.remove("open");
        modal.setAttribute("aria-hidden", "true");
        document.body.classList.remove("ua-lock");
    }

    modal.addEventListener("click", function (e) {
        if (e.target === modal || e.target.closest("[data-ua-close]")) hideModal();
    });
    document.addEventListener("keydown", function (e) { if (e.key === "Escape") hideModal(); });

    /* ---- calendar ---- */
    function openAvailability() {
        if (!cal.sel) {
            for (var n = 0; n < 120 && !cal.sel; n++) {
                var c = new Date(today); c.setDate(c.getDate() + n);
                if (availableDays.indexOf(c.getDay()) >= 0) cal.sel = dkey(c);
            }
        }
        var sd = parseKey(cal.sel || dkey(today));
        cal.y = sd.getFullYear(); cal.m = sd.getMonth();
        renderCal();
        showModal();
    }

    function renderCal() {
        var first = new Date(cal.y, cal.m, 1);
        var total = Math.ceil((first.getDay() + new Date(cal.y, cal.m + 1, 0).getDate()) / 7) * 7;
        var html = "";

        $("cal-title").textContent = first.toLocaleDateString("en-US", { month: "long", year: "numeric" });
        $("cal-badge").textContent = availableDays.length ? "Owner schedule" : "No schedule provided";
        $("cal-badge").classList.toggle("d-none", availableDays.length > 0);

        for (var n = 0; n < total; n++) {
            var d = new Date(cal.y, cal.m, 1 - first.getDay() + n), k = dkey(d);
            var cls = (d.getMonth() !== cal.m || d < today || availableDays.indexOf(d.getDay()) < 0) ? "off" : "free";
            if (k === cal.sel && cls === "free") cls += " sel";
            html += '<button type="button" class="cal-cell ' + cls + '" data-date="' + k + '"' +
                (cls.indexOf("free") === 0 ? "" : " disabled") + '><span>' + d.getDate() + '</span><i class="cal-dot"></i></button>';
        }
        $("cal-days").innerHTML = html;

        var monthsAhead = (cal.y - today.getFullYear()) * 12 + cal.m - today.getMonth();
        $("cal-prev").disabled = monthsAhead <= 0;
        $("cal-next").disabled = monthsAhead >= MAX_MONTHS_AHEAD;

        $("sel-text").textContent = cal.sel ? "Selected: " + parseKey(cal.sel).toLocaleDateString("en-US",
            { weekday: "long", month: "short", day: "numeric", year: "numeric" }) : "No available dates in the owner's schedule";
        $("av-confirm-text").textContent = cal.sel ? "Request to Rent for " + fmtShort(cal.sel) : "No available date";
    }

    $("cal-days").addEventListener("click", function (e) {
        var b = e.target.closest(".cal-cell.free");
        if (!b) return;
        cal.sel = b.getAttribute("data-date");
        renderCal();
    });
    $("cal-prev").addEventListener("click", function () { cal.m--; if (cal.m < 0) { cal.m = 11; cal.y--; } renderCal(); });
    $("cal-next").addEventListener("click", function () { cal.m++; if (cal.m > 11) { cal.m = 0; cal.y++; } renderCal(); });

    /* ---- request ---- */
    function submitRequest() {
        if (!cfg.requestUrl || !cal.sel) return;
        window.location.assign(cfg.requestUrl);
    }

    $("av-confirm").addEventListener("click", function () { hideModal(); submitRequest(); });

    var checkBtn = $("d-check");
    if (checkBtn) checkBtn.addEventListener("click", openAvailability);

    window.UniShareAvail = { open: openAvailability, toast: toast };
})();
