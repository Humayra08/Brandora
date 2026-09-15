(() => {
    const page = document.querySelector('.campaigns-page');
    page.querySelectorAll('.campaigns-filters select').forEach(select => {
        select.addEventListener('change', () => select.form.requestSubmit());
    });
    page.querySelectorAll('[data-dialog]').forEach(button => {
        button.addEventListener('click', () => document.getElementById(button.dataset.dialog).showModal());
    });
    page.querySelectorAll('dialog').forEach(dialog => {
        dialog.addEventListener('click', event => {
            const bounds = dialog.getBoundingClientRect();
            if (event.target === dialog && (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom)) {
                dialog.close();
            }
        });
    });
    page.querySelectorAll('.campaigns-image img').forEach(image => {
        const fallback = () => { image.hidden = true; };
        image.addEventListener('error', fallback);
        if (image.complete && !image.naturalWidth) fallback();
    });
})();