function scaleProgress() {
    const wrapper = document.querySelector('.progress-wrapper');

    if (!wrapper) {
        return;
    }

    const scale = Math.min(
        window.innerWidth / 500,
        window.innerHeight / 500
    );

    wrapper.style.transform = `scale(${scale})`;
}

window.dialog = {
    open: function (id) {
        const dialog = document.getElementById(id);

        if (dialog) {
            dialog.showModal();
        }
    },

    close: function (id) {
        const dialog = document.getElementById(id);

        if (dialog) {
            dialog.close();
        }
    }
};

window.addEventListener('resize', scaleProgress);
window.addEventListener('load', scaleProgress);