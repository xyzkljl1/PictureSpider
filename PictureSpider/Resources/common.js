var loading_timer;
const domain2 = 'gold-usergeneratedcontent.net';
//不使用dev.hitomi.la
var domain = 'ltn.' + domain2;
var galleryblockextension = '.html';
var galleryblockdir = 'galleryblock';
var nozomiextension = '.nozomi';
var gg = {};
var is_safari = false;//['iPad Simulator', 'iPhone Simulator', 'iPod Simulator', 'iPad', 'iPhone', 'iPod'].includes(navigator.platform) || (navigator.userAgent.includes("Mac") && "ontouchend" in document);///(\s|^)AppleWebKit\/[\d\.]+\s+\(.+\)\s+Version\/(1[0-9]|[2-9][0-9]|\d{3,})(\.|$|\s)/i.test(navigator.userAgent);

function subdomain_from_url(url, base, dir) {
    var retval = '';
    if (!base) {
        if (dir === 'webp') {
            retval = 'w';
        } else if (dir === 'avif') {
            retval = 'a';
        }
    }

    var b = 16;

    var r = /\/[0-9a-f]{61}([0-9a-f]{2})([0-9a-f])/;
    var m = r.exec(url);
    if (!m) {
        return retval;
    }

    var g = parseInt(m[2] + m[1], b);
    if (!isNaN(g)) {
        if (base) {
            retval = String.fromCharCode(97 + gg.m(g)) + base;
        } else {
            retval = retval + (1 + gg.m(g));
        }
    }

    return retval;
}

function url_from_url(url, base, dir) {
    return url.replace(/\/\/..?\.(?:gold-usergeneratedcontent\.net|hitomi\.la)\//, '//' + subdomain_from_url(url, base, dir) + '.' + domain2 + '/');
}


function full_path_from_hash(hash) {
    return gg.b + gg.s(hash) + '/' + hash;
}

function real_full_path_from_hash(hash) {
    return hash.replace(/^.*(..)(.)$/, '$2/$1/' + hash);
}


function url_from_hash(galleryid, image, dir, ext) {
    ext = ext || dir || image.name.split('.').pop();
    if (dir === 'webp' || dir === 'avif') {
        dir = '';
    } else {
        dir += '/';
    }

    return 'https://a.' + domain2 + '/' + dir + full_path_from_hash(image.hash) + '.' + ext;
}

function url_from_url_from_hash(galleryid, image, dir, ext, base) {
    if ('tn' === base) {
        return url_from_url('https://a.' + domain2 + '/' + dir + '/' + real_full_path_from_hash(image.hash) + '.' + ext, base);
    }
    return url_from_url(url_from_hash(galleryid, image, dir, ext), base, dir);
}

function rewrite_tn_paths(html) {
    return html.replace(/\/\/tn\.hitomi\.la\/[^\/]+\/[0-9a-f]\/[0-9a-f]{2}\/[0-9a-f]{64}/g, function (url) {
        return url_from_url(url, 'tn');
    });
}


function show_loading() {
    return vate_loading(true);
}

function hide_loading() {
    stop_loading_timer();
    return vate_loading(false);
}

function vate_loading(bool) {
    var el = $('#loader-content');
    if (!el) return;

    if (bool) {
        el.show();
    } else {
        el.hide();
    }
}


function start_loading_timer() {
    hide_loading();
    loading_timer = setTimeout(show_loading, 500);
}

function stop_loading_timer() {
    clearTimeout(loading_timer);
}



function scroll_to_top() {
    document.body.scrollTop = document.documentElement.scrollTop = 0;
}


function localDates() {
    let locale;// = 'ja-JP';
    $(".date").each(function () {
        //2007-02-06 20:02:00-06
        //2016-03-27 13:37:33.612-05
        let m = /(\d{4})-(\d{2})-(\d{2}) (\d{2}):(\d{2}):(\d{2})(?:\.\d+)?([+-]\d{2})/.exec($(this).html());
        if (!m) {
            //2016-03-27
            m = /(\d{4})-(\d{2})-(\d{2})/.exec($(this).html());
            if (!m) {
                return;
            }
            $(this).html(new Date(m[1] + '-' + m[2] + '-' + m[3]).toLocaleString(locale, { timeZone: 'UTC', year: 'numeric', month: 'short', day: 'numeric' }));
            return;
        }
        //2007-02-06T20:02:00-06:00
        $(this).html(new Date(m[1] + '-' + m[2] + '-' + m[3] + 'T' + m[4] + ':' + m[5] + ':' + m[6] + m[7] + ':00').toLocaleString(locale, { year: 'numeric', month: 'short', day: 'numeric', hour: 'numeric', minute: 'numeric' })); //Feb 6, 2007, 8:02 PM
    });
}

//https://stackoverflow.com/a/51332115/272601
function retry(fn, retries, err) {
    retries = typeof retries !== 'undefined' ? retries : 3;
    err = typeof err !== 'undefined' ? err : null;

    if (!retries) {
        return Promise.reject(err);
    }
    return fn().catch(function (err) {
        //console.warn(`retry ${3 - retries}, err ${err}`);
        return retry(fn, (retries - 1), err);
    });
}


function flip_lazy_images() {
    const sources = document.querySelectorAll('source.picturelazyload');
    sources.forEach(function (lazyEl) {
        lazyEl.setAttribute("srcset", lazyEl.getAttribute("data-srcset"));
    });

    const imgs = document.querySelectorAll('img.lazyload');
    imgs.forEach(function (lazyEl) {
        lazyEl.setAttribute("src", lazyEl.getAttribute("data-src"));
        //lazyEl.setAttribute("srcset", lazyEl.getAttribute("data-srcset")); //can't do this because the webp shim can't handle it
    });
}

function is_webtoon_aspect_ratio(width, height) {
    return (height / width >= 3);
}

function sanitize_gallery_title(str) {
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&apos;');
}

function set_keywords() {
    let keywords = [];

    if (typeof galleryinfo !== 'undefined' && galleryinfo['tags']) {
        keywords = galleryinfo['tags'].map(tag => tag.tag);
    } else if (typeof tag_display !== 'undefined' && tag_display) {
        keywords = [tag_display];
    }

    [].forEach.call(['eas6a97888e', 'adsbyexoclick'], (class_name) => {
        let inses = document.getElementsByClassName(class_name);
        [].forEach.call(inses, (el) => {
            el.setAttribute('data-keywords', keywords.join(','));
        });
    });

    return keywords;
}

function hitomic(provider_code, category_name = 'default') {
    const buster = Math.round(new Date().getTime() / 1000);
    $.ajax({ url: `//master.hitomi.la/hitomic/${provider_code}/${category_name}/${buster}` });
}

function inc_cookie(cookie_name) {
    let c = parseInt(Cookies.get(cookie_name), 10) || 0;
    c++;

    let t = new Date(new Date().getTime() + 30 * 60 * 1000);
    const date_name = cookie_name + '_date';
    if (Cookies.get(date_name)) {
        t = new Date(Cookies.get(date_name));
    }

    Cookies.set(cookie_name, c, { secure: true, expires: t });
    Cookies.set(date_name, t, { secure: true, expires: t });
}

if (typeof Cookies !== 'undefined' && Cookies) {
    if (!/\/(?:search|all)[^\/]*\.html/.test(window.location.href)) {
        inc_cookie('a0e');
        if (!/\/reader\//.test(window.location.href)) {
            inc_cookie('pvp');
        }
    }
    let set_pve = (val) => { Cookies.set('pve', val, { secure: true, expires: 1 }); };
    fetch('https://pagead2.googlesyndication.com/pagead/show_ads.js', { mode: 'no-cors' }).then(() => { set_pve('1'); }).catch((err) => { set_pve('0'); });

    if (Cookies.get('observe_cls')) {
        let cls = 0;
        const po = new PerformanceObserver((list) => {
            for (const entry of list.getEntries()) {
                cls += entry.value;
                console.log(entry, 'total is now: ' + cls);
            }
        });
        po.observe({ type: 'layout-shift', buffered: true });
    }
}

function mark_unread(datas) {
    if (typeof Cookies === 'undefined' || !Cookies) return;

    var last_visited_epoch, last_visited_date, match, galleryblock_date;

    if (last_visited_epoch = Cookies.get('last_visited_recently') || Cookies.get('last_visited_epoch')) {
        if (last_visited_date = new Date(parseInt(last_visited_epoch))) {
            var mark = -1;
            for (var i = 0; i < datas.length; i++) {
                if (match = datas[i].match(/\sdata-posted="([^"]+)/)) {
                    if (galleryblock_date = new Date(match[1])) {
                        if (galleryblock_date > last_visited_date) {
                            mark = i;
                        }
                    }
                }
            }
            if (mark >= 0 && mark < datas.length - 1 && !/\/(?:date|popular)\//.test(window.location.href)) {
                datas[mark] += '<hr class="unread">';
            }
        }
    }

    if (!Cookies.get('last_visited_recently')) {
        Cookies.set('last_visited_recently', Cookies.get('last_visited_epoch') || 1, { secure: true, expires: 1 / 24 / 3 });
        Cookies.set('last_visited_epoch', new Date().getTime(), { secure: true, expires: 365 * 10 });
    }
}