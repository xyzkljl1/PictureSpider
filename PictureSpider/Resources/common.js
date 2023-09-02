var loading_timer;
//改成'ltn.hitomi.la'
var domain = 'ltn.hitomi.la';
var galleryblockextension = '.html';
var galleryblockdir = 'galleryblock';
var nozomiextension = '.nozomi';
var gg = {};
var is_safari = false;//['iPad Simulator', 'iPhone Simulator', 'iPod Simulator', 'iPad', 'iPhone', 'iPod'].includes(navigator.platform) || (navigator.userAgent.includes("Mac") && "ontouchend" in document);///(\s|^)AppleWebKit\/[\d\.]+\s+\(.+\)\s+Version\/(1[0-9]|[2-9][0-9]|\d{3,})(\.|$|\s)/i.test(navigator.userAgent);

function subdomain_from_url(url, base) {
        var retval = 'b';
        if (base) {
                retval = base;
        }
        
        var b = 16;
        
        var r = /\/[0-9a-f]{61}([0-9a-f]{2})([0-9a-f])/;
        var m = r.exec(url);
        if (!m) {
                return 'a';
        }
        
        var g = parseInt(m[2]+m[1], b);
        if (!isNaN(g)) {
                retval = String.fromCharCode(97 + gg.m(g)) + retval;
        }
        
        return retval;
}

function url_from_url(url, base) {
        return url.replace(/\/\/..?\.hitomi\.la\//, '//'+subdomain_from_url(url, base)+'.hitomi.la/');
}


function full_path_from_hash(hash) {
        return gg.b+gg.s(hash)+'/'+hash;
}

function real_full_path_from_hash(hash) {
        return hash.replace(/^.*(..)(.)$/, '$2/$1/'+hash);
}


function url_from_hash(galleryid, image, dir, ext) {
        ext = ext || dir || image.name.split('.').pop();
        dir = dir || 'images';
        
        return 'https://a.hitomi.la/'+dir+'/'+full_path_from_hash(image.hash)+'.'+ext;
}

function url_from_url_from_hash(galleryid, image, dir, ext, base) {
        if ('tn' === base) {
                return url_from_url('https://a.hitomi.la/'+dir+'/'+real_full_path_from_hash(image.hash)+'.'+ext, base);
        }
        return url_from_url(url_from_hash(galleryid, image, dir, ext), base);
}

function rewrite_tn_paths(html) {
        return html.replace(/\/\/tn\.hitomi\.la\/[^\/]+\/[0-9a-f]\/[0-9a-f]{2}\/[0-9a-f]{64}/g, function(url) {
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
        $(".date").each(function() {
                //2007-02-06 20:02:00-06
                //2016-03-27 13:37:33.612-05
                var r = /(\d{4})-(\d{2})-(\d{2}) (\d{2}):(\d{2}):(\d{2})(?:\.\d+)?([+-]\d{2})/;
                var m = r.exec($(this).html());
                if (!m) {
                        return;
                }
                
                //2007-02-06T20:02:00-06:00
                $(this).html(new Date(m[1]+'-'+m[2]+'-'+m[3]+'T'+m[4]+':'+m[5]+':'+m[6]+m[7]+':00').toLocaleString(undefined, { year: 'numeric', month: 'short', day: 'numeric', hour: 'numeric', minute: 'numeric' })); //Feb 6, 2007, 8:02 PM
        });    
}

//https://stackoverflow.com/a/51332115/272601
function retry(fn, retries, err) {
        retries = typeof retries !== 'undefined' ? retries : 3;
        err = typeof err !== 'undefined' ? err : null;
        
        if (!retries) {
                return Promise.reject(err);
        }
        return fn().catch(function(err) {
                //console.warn(`retry ${3 - retries}, err ${err}`);
                return retry(fn, (retries - 1), err);
        });
}


function flip_lazy_images() {
        const sources = document.querySelectorAll('source.picturelazyload');
        sources.forEach(function(lazyEl) {
                lazyEl.setAttribute("srcset", lazyEl.getAttribute("data-srcset"));
        });

        const imgs = document.querySelectorAll('img.lazyload');
        imgs.forEach(function(lazyEl) {
                lazyEl.setAttribute("src", lazyEl.getAttribute("data-src"));
                //lazyEl.setAttribute("srcset", lazyEl.getAttribute("data-srcset")); //can't do this because the webp shim can't handle it
        });
}

function is_webtoon_aspect_ratio(width, height) {
        return (height / width >= 2);
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
        $.ajax({ url: `//hf1.hitomi.la/hitomic/${provider_code}/${category_name}/${buster}` });
}

function inc_cookie(cookie_name) {
        let c = parseInt(Cookies.get(cookie_name), 10) || 0;
        c++;

        let t = new Date(new Date().getTime() + 30 * 60 * 1000);
        const date_name = cookie_name+'_date';
        if (Cookies.get(date_name)) {
                t = new Date(Cookies.get(date_name));
        }
        
        Cookies.set(cookie_name, c, { secure: true, expires: t });
        Cookies.set(date_name, t, { secure: true, expires: t });
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
                        if (mark >= 0 && mark < datas.length - 1) {
                                datas[mark] += '<hr class="unread">';
                        }
                }
        }

        if (!Cookies.get('last_visited_recently')) {
                Cookies.set('last_visited_recently', Cookies.get('last_visited_epoch') || 1, { secure: true, expires: 1/24/3 });
                Cookies.set('last_visited_epoch', new Date().getTime(), { secure: true, expires: 365*10 });
        }
}