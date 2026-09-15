(() => {
    const page = document.querySelector('.campaign-apply-page');
    if (!page) return;

    const bindCounter = (fieldId, countId) => {
        const field = document.getElementById(fieldId);
        const count = document.getElementById(countId);
        if (!field || !count) return;
        field.addEventListener('input', () => { count.textContent = field.value.length; });
    };
    bindCounter('concept-field', 'concept-count');
    bindCounter('whyfit-field', 'whyfit-count');

    page.querySelectorAll('.apply-social-remove').forEach(button => {
        button.addEventListener('click', () => { button.closest('li').hidden = true; });
    });
})();
