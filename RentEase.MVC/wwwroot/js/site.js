// Auto-dismiss alerts after 4 seconds
document.addEventListener("DOMContentLoaded", function () {
    setTimeout(function () {
        document.querySelectorAll(".alert-dismissible").forEach(function (el) {
            var bsAlert = bootstrap.Alert.getOrCreateInstance(el);
            bsAlert.close();
        });
    }, 4000);

    // Confirm delete handled globally by #globalConfirmModal in _Layout.cshtml
});
