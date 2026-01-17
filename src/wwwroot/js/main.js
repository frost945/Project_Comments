const config = {
    pageSize: 25
};
const state = {
    currentParentId: null,
    isLoading: false,
    noMoreReplies: false,

    skipComments: 0,
    pageNumber: 1,
    skipReplies: 0,

    sortField: "CreatedAt",
    sortDir: true, // true - ascending, false - descending
    mainCaptchaCode: null
}
const dom = {
    commentForm: document.getElementById('commentForm'),
    containerComments: document.getElementById('comments-containerId'),
    repliesPage: document.getElementById('repliesPage'),
    repliesContainer: document.getElementById('repliesContainer')
};

function setupEventHandlers()
{
    document.getElementById('openAddCommentModal').addEventListener('click', () => {
        openModal();
    });

    // Event listeners for pagination root comments
    document.getElementById('nextBtn')?.addEventListener('click', async () => {
        state.skipComments += config.pageSize;
        ++state.pageNumber;
        sessionStorage.setItem('pageNumber', state.pageNumber);

        await initComments();
        window.scrollTo({top: 0});
        console.debug("Updated next skipComments to:", state.skipComments);
    });

    document.getElementById('prevBtn')?.addEventListener('click', async () => {
        if (state.skipComments === 0) return;

        state.skipComments -= config.pageSize;
        if (state.skipComments < 0)
            state.skipComments = 0;

        --state.pageNumber;
        sessionStorage.setItem('pageNumber', state.pageNumber);

        if (state.pageNumber < 1)
            state.pageNumber = 1;

        await initComments();
        window.scrollTo({ top: 0 });
        console.debug("Updated back skipComments to:", state.skipComments);
    });

    // Sort buttons
    document.querySelectorAll('.sort-btn').forEach(btn => {
        btn.addEventListener('click', async () => {
            state.sortField = btn.dataset.field;
            sessionStorage.setItem('sortField', state.sortField);
            sessionStorage.setItem('sortTouched', 'true');

            resetPagination();
            document.querySelectorAll('.sort-btn').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');

            await initComments();
        });
    });

    // Sort direction toggle
    document.getElementById('dirToggle')?.addEventListener('click', async () => {
        state.sortDir = !state.sortDir;
        sessionStorage.setItem('sortDir', state.sortDir);
        sessionStorage.setItem('sortTouched', 'true');

        const btn = document.getElementById('dirToggle');
        btn.textContent = state.sortDir ? "↑" : "↓";

        await initComments();
    });

    document.addEventListener('click', (e) => {
        const link = e.target.closest('.comment-image-link');
        if (!link) return;

        link.blur(); //remove focus before opening
    });
}

function savedState()
{
    const savedPageNumber = Number(sessionStorage.getItem('pageNumber'));

    state.sortField = sessionStorage.getItem('sortField') || "CreatedAt";

    const savedSortDir = sessionStorage.getItem('sortDir');
    state.sortDir = savedSortDir !== null ? savedSortDir === 'true' : true;

    // if sorting has been touched
    const sortTouched = sessionStorage.getItem('sortTouched') === 'true';

    if (sortTouched)
    {
        const btnSort = document.querySelector(`.sort-btn[data-field="${state.sortField}"]`);

        if (btnSort) {
            document.querySelectorAll('.sort-btn.active')
                .forEach(b => b.classList.remove('active'));

            btnSort.classList.add('active');
        }
        
        const btnDir = document.getElementById('dirToggle');

        if (btnDir)
            btnDir.textContent = state.sortDir ? "↑" : "↓";
    }

    if (Number.isInteger(savedPageNumber) && savedPageNumber >= 1) {
        state.pageNumber = savedPageNumber;
        state.skipComments = (state.pageNumber - 1) * config.pageSize;
    }
}

async function restoreRepliesState()
{
    const parentId = sessionStorage.getItem('currentParentId');
    if (parentId)
    {
        await openReplies(parentId);
    }

}

function openModal()
{
    const addCommentModal = document.getElementById('addCommentModal');

    addCommentModal.classList.remove('hidden');
    document.body.style.overflow = 'hidden';
    // bind form submit event only once
    if (!dom.commentForm.dataset.bound) {
        dom.commentForm.addEventListener('submit', onSubmitCommentForm);
        dom.commentForm.dataset.bound = 'true';
    }

    const closeAddCommentModal = addCommentModal.querySelector('.close');
    closeAddCommentModal.addEventListener('click', () => {
        addCommentModal.classList.add('hidden');
        document.body.style.overflow = '';
    });

    window.addEventListener('click', (event) => {
        if (event.target === addCommentModal) {
            addCommentModal.classList.add('hidden');
            document.body.style.overflow = '';
        }
    });

    initFileUpload();
    initCaptcha();
}

async function loadRootComments()
{
    const response = await fetch(`/Comment/parent?skip=${state.skipComments}&sortBy=${state.sortField}&ascending=${state.sortDir}`);
    const comments = await response.json();

    console.debug("Loaded root comments:", comments);

    if (!comments || comments.length === 0)
    {
        console.debug("No comments for loading");
        return;
    }
    return comments;
}

function displayComment(comment, isRoot=true)
{
    const commentDiv = document.createElement('div');
    commentDiv.classList.add('comment-card');

    commentDiv.setAttribute('data-comment-id', comment.id);
    commentDiv.dataComment = comment;

    const header = document.createElement('div');
    header.classList.add('comment-header');

    const userNameElem = document.createElement('span');
    userNameElem.classList.add('comment-username');
    userNameElem.textContent = comment.userName || 'Неизвестный пользователь';

    const dateElem = document.createElement('span');
    dateElem.classList.add('comment-date');
    dateElem.textContent = comment.createdAt || '';

    header.appendChild(userNameElem);
    header.appendChild(dateElem);

    if (isRoot)
    {
        const repliesBtn = document.createElement('button');
        repliesBtn.classList.add('replies-btn');

        repliesBtn.innerHTML = `
        <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
            <path d="M20 2H4c-1.1 0-1.99.9-1.99 2L2 22l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-2 12H6v-2h12v2zm0-3H6V9h12v2zm0-3H6V6h12v2z"/>
        </svg>
        Ответы
    `;
        repliesBtn.addEventListener('click', () => {
             openReplies(comment.id);
        });
        header.appendChild(repliesBtn);
    }

    const textElem = document.createElement('p');
    textElem.classList.add('comment-text');
    textElem.innerHTML = sanitizeHtml(comment.text);

    commentDiv.appendChild(header);
    commentDiv.appendChild(textElem);

    // add image file if exists
    if (comment.imagePreviewUrl)
    {
        const imageContainer = displayImageFile(comment.imagePreviewUrl, comment.imageOriginalUrl); 
        commentDiv.appendChild(imageContainer);
    }
    // add text file if exists
    if (comment.textFileUrl)
    {
        const textFileContainer = displayTextFile(comment.textFileUrl, comment.textFileName);
        commentDiv.appendChild(textFileContainer);
    }
    return commentDiv;
}

function handlePaginationState(comments)
{
    const nextBtn = document.getElementById('nextBtn');
    const pageNumberElem = document.getElementById('pageNumber');

    if (!comments || comments.length === 0)
    {
        console.debug("No comments");

        if (state.skipComments >= config.pageSize) {
            state.skipComments -= config.pageSize;
            --state.pageNumber;
            sessionStorage.setItem('pageNumber', state.pageNumber);
        }
        console.debug("state.pageNumber:", state.pageNumber);
        nextBtn.disabled = true;
        pageNumberElem.innerText = state.pageNumber;
        return false;
    }

    nextBtn.disabled = false;
    pageNumberElem.innerText = state.pageNumber;
    console.debug("state.pageNumber:", state.pageNumber);

    return true;
}

async function initComments()
{
    const comments = await loadRootComments();

    const shouldRender = handlePaginationState(comments);
    if (!shouldRender) return;

    dom.containerComments.innerHTML = '';

    comments.forEach(comment => {
        const commentElem = displayComment(comment, true);
        dom.containerComments.appendChild(commentElem);
    });

    initLightbox();
}

function displayImageFile(imagePreviewUrl, imageOriginalUrl)
{
    const imageContainer = document.createElement('div');
    const link = document.createElement('a');

    link.href = imageOriginalUrl;
    link.classList.add('comment-image-link');
    link.setAttribute('data-gallery', imagePreviewUrl);


    const img = document.createElement('img');
    img.src = imagePreviewUrl;
    img.alt = 'Изображение комментария';
    img.classList.add('comment-image');

    link.appendChild(img);
    imageContainer.appendChild(link);
    return imageContainer;
}

function displayTextFile(textFileUrl, textFileName)
{
    console.debug("textFileUrl= ", textFileUrl);

    const textFileContainer = document.createElement('div');
    textFileContainer.classList.add('comment-textfile-container');

    // Создаем ссылку для скачивания файла
    const downloadLink = document.createElement('a');
    downloadLink.href = textFileUrl;
    downloadLink.target = '_blank';
    downloadLink.rel = 'noopener noreferrer';
    downloadLink.classList.add('text-file-link');
    downloadLink.setAttribute('download', textFileName || 'file.txt');
    downloadLink.innerHTML = `
            <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
                <path d="M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z"/>
            </svg>
             (${textFileName || 'file.txt'})
        `;
    textFileContainer.appendChild(downloadLink);
    return textFileContainer;
}

function resetPagination() {
    state.skipComments = 0;
    state.pageNumber = 1;
    sessionStorage.setItem('pageNumber', state.pageNumber);
}
function resetRepliesPagination() {
    state.skipReplies = 0;
    state.noMoreReplies = false;
}

function updateTitleModal(isReplyMode)
{
    const titleModal = document.querySelector('.modal-title');
    titleModal.textContent = isReplyMode
        ? titleModal.dataset.replyText
        : titleModal.dataset.defaultText;
}
function showMainHeader() {
    document.getElementById("mainHeader").style.display = "block";
    document.getElementById("repliesHeader").style.display = "none";
}
function showRepliesHeader() {
    document.getElementById("mainHeader").style.display = "none";
    document.getElementById("repliesHeader").style.display = "block";
}

async function loadReplies(parentId)
{
    if (state.isLoading || state.noMoreReplies) return;
    state.isLoading = true;

    console.debug(`Loading replies for parentId: ${parentId}, skipReplies: ${state.skipReplies}`);
    try
    {
        const response = await fetch(`/Comment/children?skip=${state.skipReplies}&parentId=${parentId}`);
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        const replies = await response.json();
        console.debug(replies);

        if (replies.length === 0)
        {
            console.debug("Больше нет сообщений для загрузки");
            state.noMoreReplies = true;
            return;
        }

        return replies;
    }
    catch (error) {
        console.error('Error loading replies:', error);
        dom.repliesContainer.innerHTML = '<div class="no-replies">Ошибка загрузки ответов</div>';
    }
    finally {
        state.isLoading = false;
    }
}

async function initReplies()
{
    const replies = await loadReplies(state.currentParentId);

    if (!replies || replies.length === 0) return;

    replies.forEach(reply => {
        const replyElem = displayComment(reply, false);
        dom.repliesContainer.appendChild(replyElem);
    });

    state.skipReplies += config.pageSize;
    console.debug("Updated skipReplies to:", state.skipReplies);

    initLightbox();
}

//loading replies when scrolling down
function handleRepliesScroll()
{
    const rect = dom.repliesPage.getBoundingClientRect();
    // проверяем, виден ли низ  и насколько проскроллили
    const reachedBottom = rect.bottom <= window.innerHeight + 5;

    if (reachedBottom) {
        console.log("Достигнут низ repliesPage через body-scroll");
        initReplies();
    }
}

async function openReplies(parentId) {
    showRepliesHeader();
    updateTitleModal(true);

    state.currentParentId = parentId;
    sessionStorage.setItem('currentParentId', parentId);

    const openAddReplyModal = document.getElementById('openAddReplyModal');
    openAddReplyModal.addEventListener('click', () => {
        openModal();
    });

    dom.repliesPage.classList.remove('hidden');
    dom.containerComments.classList.add('hidden');
    document.getElementById('pagination-page').classList.add('hidden');

    //Adding a scroll handler when opening replies
    window.addEventListener("scroll", handleRepliesScroll);

    dom.repliesContainer.innerHTML = '';

    console.debug("Opening replies modal for comment ID:", state.currentParentId);

    const commentEl = document.querySelector(`.comment-card[data-comment-id="${parentId}"]`);
    console.debug("Found comment element for replies:", commentEl);
    const comment = commentEl.dataComment;
    console.debug("Parent comment data in openReplies:", comment);
    if (comment)
    {
        const parentComment = displayComment(comment, false);//comment is displayed without a reply button

        const parentCommentEl = document.getElementById("originalComment");
        parentCommentEl.innerHTML = "";
        parentCommentEl.appendChild(parentComment);
    }

    await initReplies();

    const closeRepliesBtn = document.querySelector('.close-replies');
    closeRepliesBtn.addEventListener('click', () => {
        closeReplies();
    });
}

function closeReplies()
{
    dom.repliesPage.classList.add('hidden');
    dom.containerComments.classList.remove('hidden');
    document.getElementById('pagination-page').classList.remove('hidden');

    state.currentParentId = null;
    sessionStorage.removeItem('currentParentId');
    
    window.removeEventListener("scroll", handleRepliesScroll);

    resetRepliesPagination();
    updateTitleModal(false);
    showMainHeader();
}

// comment  form handler
async function onSubmitCommentForm(event)
{
    event.preventDefault();

    const commentText = document.getElementById('commentText').value;
    if (!validateCommentText(commentText)) return;

    const userName = document.getElementById('userName').value;
    if (!validateUsername(userName)) return;

    if (!validateFormCaptcha()) return;

    const submitBtn = dom.commentForm.querySelector('.submit-btn');
    const originalText = submitBtn.textContent;

    try {
        submitBtn.disabled = true;

        const formData = createCommentFormData();

        const comment = await sendComment(formData);

        await handleCommentSuccess(comment);

    }
    catch (error) {
        handleCommentError(error)
    }
    finally {
        submitBtn.disabled = false;
        submitBtn.textContent = originalText;
    }
}

function createCommentFormData() {
    const formData = new FormData();

    formData.append('userName', document.getElementById('userName').value);
    formData.append('email', document.getElementById('email').value);
    formData.append('text', document.getElementById('commentText').value);
    formData.append('parentId', state.currentParentId ?? '');

    const file = document.getElementById('file').files[0];
    if (file) {
        formData.append('file', file);
    }

    return formData;
}

async function sendComment(formData) {
    const response = await fetch('/Comment', {
        method: 'POST',
        body: formData
    });

    if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || `HTTP error! status: ${response.status}`);
    }

    return await response.json();
}

async function handleCommentSuccess(comment) {
    refreshMainCaptcha();
    document.getElementById('captchaInput').value = '';
    document.getElementById('captchaInput').classList.remove('captcha-error', 'captcha-success');

    document.getElementById('addCommentModal').classList.add('hidden');
    //addCommentModal.classList.add('hidden');

    document.body.style.overflow = '';
    dom.commentForm.reset();

    document.getElementById('preview').innerHTML = '';

    resetPagination();

    if (state.currentParentId) {
        dom.repliesContainer.appendChild(displayComment(comment, false));
    } else {
        await initComments();
    }

    initLightbox();
}

function handleCommentError(error) {
    console.error('Ошибка при отправке комментария:', error);
    alert('Произошла ошибка при отправке комментария: ' + error.message);
}

function initCaptcha()
{
    refreshMainCaptcha();
    setupCaptchaEventHandlers();
}

function refreshMainCaptcha() {
    state.mainCaptchaCode = generateCaptchaText(5);
    displayCaptcha('captchaContainer', state.mainCaptchaCode);
    document.getElementById('captchaInput').value = '';
    document.getElementById('captchaInput').classList.remove('captcha-error', 'captcha-success');
}

function generateCaptchaText(length = 5) {
    const chars = 'abcdefghjkmnpqrstuvwxyz23456789';
    let result = '';
    for (let i = 0; i < length; i++) {
        result += chars.charAt(Math.floor(Math.random() * chars.length));
    }
    return result;
}

function displayCaptcha(containerId, text) {
    const container = document.getElementById(containerId);
    if (!container) return;

    const canvas = document.createElement('canvas');
    canvas.width = 200;
    canvas.height = 60;
    canvas.style.borderRadius = '6px';
    canvas.style.border = '1px solid #dee2e6';

    const ctx = canvas.getContext('2d');

    ctx.fillStyle = '#f8f9fa';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    for (let i = 0; i < 100; i++) {
        ctx.fillStyle = `rgba(${Math.random() * 100}, ${Math.random() * 100}, ${Math.random() * 100}, 0.1)`;
        ctx.fillRect(
            Math.random() * canvas.width,
            Math.random() * canvas.height,
            2, 2
        );
    }

    for (let i = 0; i < 5; i++) {
        ctx.strokeStyle = `rgba(${Math.random() * 100}, ${Math.random() * 100}, ${Math.random() * 100}, 0.3)`;
        ctx.beginPath();
        ctx.moveTo(Math.random() * canvas.width, Math.random() * canvas.height);
        ctx.lineTo(Math.random() * canvas.width, Math.random() * canvas.height);
        ctx.stroke();
    }
    // text drawing
    for (let i = 0; i < text.length; i++) {
        const char = text[i];
        ctx.fillStyle = `hsl(${Math.random() * 360}, 70%, 45%)`;
        ctx.font = `${30 + Math.random() * 10}px Arial`;
        ctx.save();
        ctx.translate(30 + i * 35, 50);
        ctx.rotate((Math.random() - 0.5) * 0.4);
        ctx.fillText(char, 0, 0);
        ctx.restore();
    }

    container.innerHTML = '';
    container.appendChild(canvas);
}

function setupCaptchaEventHandlers() {
    document.getElementById('refreshCaptcha')?.addEventListener('click', () => {
        refreshMainCaptcha();
    });
    // Real-time validation
    document.getElementById('captchaInput')?.addEventListener('input', (e) => {
        validateCaptchaInput(e.target, state.mainCaptchaCode);
    });
}

function validateCaptchaInput(input, correctCode)
{
    const value = input.value;
    input.classList.remove('captcha-error', 'captcha-success');

    if (!value)
        return false;

    const isValid = value.toLowerCase() === correctCode.toLowerCase();

    input?.classList.add(isValid ? 'captcha-success' : 'captcha-error');

    return isValid;
}

function validateFormCaptcha()
{
    const captchaInput = document.getElementById('captchaInput')
    const correctCode = state.mainCaptchaCode;

    if (!captchaInput)
        return false;

    if (!validateCaptchaInput(captchaInput, correctCode))
    {
        captchaInput.focus();
        refreshMainCaptcha();
        return false;
    }
    return true;
}


const SANITIZER_CONFIG = {
    ALLOWED_TAGS: ['a', 'code', 'i', 'strong'],
    ALLOWED_ATTR: ['href', 'title'],
    ALLOW_DATA_ATTR: false
};

function sanitizeHtml(input)
{
    if (typeof DOMPurify === 'undefined') {
        console.warn('DOMPurify is unavailable');
        return input;
    }
    if (!input || typeof input !== 'string') return '';

    return DOMPurify.sanitize(input, SANITIZER_CONFIG);
}

function validateHtml(input)
{
    if (!input || typeof input !== 'string')
    {
        return { isValid: true, errors: [] };
    }

    const cleaned = sanitizeHtml(input);
    const isValid = cleaned === input;

    console.debug("Cleaned HTML:", cleaned);

    return {
        isValid,
        errors: isValid ? [] : "Найден запрещённый HTML",
        cleaned
    };
}

//validation for comment form
function validateCommentText(text) {
    if (!text || typeof text !== 'string')
        return true; 

    const validation = validateHtml(text);

    if (!validation.isValid)
    {
        alert(validation.errors);
        return false;
    }
    return true;
}
function validateUsername(username)
{
    if (!username || username.includes(' ') || username.length < 3 || username.length > 20) {
        alert("Incorrect username");
        return false;
    }

    return true;
}

// handler for image/text file upload
function initFileUpload()
{
    const fileInput = document.getElementById('file');

    function handleImageSelect(input)
    {
        const file = input.files[0];

        if(!file) return;

        const allowedImageTypes = ['image/jpeg', 'image/jpg', 'image/png', 'image/gif'];
        const allowedTextTypes = ['text/plain', '.txt'];

        // Checking file type
        if (!allowedImageTypes.includes(file.type) && !allowedTextTypes.includes(file.type))
        {
            input.value = '';
            return;
        }
        // Checking image file size, up to (5MB)
        if (allowedImageTypes.includes(file.type) && file.size > 5 * 1024 * 1024) {
            alert('The image size must not exceed 5MB');
            input.value = '';
            return;
        }
        // Checking txt file size up to (100KB)
        if (allowedTextTypes.includes(file.type) && file.size > 100 * 1024)
        {
            alert('The file size must not exceed 100KB');
            input.value = '';
            return;
        }
    }

    fileInput?.addEventListener('change', () => {
        handleImageSelect(fileInput);
    });
}


/* --- Helper function: wraps the selection in a textarea--- */
function wrapSelection(textarea, openTag, closeTag) {
    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;

    const before = textarea.value.slice(0, start);
    const sel = textarea.value.slice(start, end);
    const after = textarea.value.slice(end);

    textarea.value = before + openTag + sel + closeTag + after;

    // если нет выделения — ставим курсор между тегами
    if (sel.length === 0) {
        const cursor = start + openTag.length;
        textarea.selectionStart = textarea.selectionEnd = cursor;
    }
    else {
        // если был выделенный текст — ставим курсор после закрывающего тега
        const cursor = start + openTag.length + sel.length + closeTag.length;
        textarea.selectionStart = textarea.selectionEnd = cursor;
    }

    textarea.focus();
}

/* ---Local preview on the client--- */
function clientSidePreview(raw)
{
    if (!raw) return '';

    const validation = validateHtml(raw);

    if (!validation.isValid) 
        console.warn(validation.errors);

    const safe = validation.cleaned;
    const wrapper = document.createElement('div');
    wrapper.innerHTML = safe;

    return wrapper.innerHTML;
}


/* --- Attaching buttons to wrap--- */
document.querySelectorAll('.block-tags .btn[data-tag]').forEach(btn =>
{
    btn.addEventListener('click', () => {
        const tag = btn.getAttribute('data-tag');
        const textArea = document.getElementById('commentText');
        wrapSelection(textArea, `<${tag}>`, `</${tag}>`);
    });
});

/* --- Link: Request href and title from the user, or insert a template --- */
document.getElementById('btn-link').addEventListener('click', async () =>
{
    const ta = document.getElementById('commentText');
    let selection = ta.value.slice(ta.selectionStart, ta.selectionEnd);

    // Вариант: диалог с пользователем (можно заменить кастомным модальным окном)
    const href = prompt('URL (href) — полный, например https://example.com', 'https://');
    if (!href) return;
    console.debug('User provided href for link:', href);
    
    let title = prompt('Title (необязательно)', '');
    title = title ? ` title="${title.replace(/"/g, '&quot;')}"` : '';

    if (!selection)
        selection = href;
    console.debug('Current selection for link:', selection);

    const open = `<a href="${href.replace(/"/g, '&quot;')}"${title}>`;
    const close = '</a>';

    wrapSelection(ta, open, selection+close);
});

document.getElementById('previewBtn').addEventListener('click', async () =>
{
    const raw = document.getElementById('commentText').value
    const previewDiv = document.getElementById('preview');
    previewDiv.innerHTML = clientSidePreview(raw);
});

let lightboxInstance = null;
function initLightbox() {
    if (lightboxInstance) {
        lightboxInstance.destroy();
    }

    lightboxInstance = GLightbox({
        selector: '.comment-image-link',
        slideEffect: 'none',
        keyboardNavigation: false,
        touchNavigation: false,
        draggable: false,
        loop: false,
        zoomable: true,
        closeButton: true,
        closeOnOutsideClick: true
    });
}

// Initialize main page
document.addEventListener('DOMContentLoaded', async () =>
{
    setupEventHandlers();
    savedState();
    await initComments();
    restoreRepliesState();
});