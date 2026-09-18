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

window.addEventListener('resize', scaleProgress);
window.addEventListener('load', scaleProgress);