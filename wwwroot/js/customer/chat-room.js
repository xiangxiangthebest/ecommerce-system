document.addEventListener("DOMContentLoaded", function () {
    var messageBody = document.getElementById("messageBody");
    if (messageBody) { messageBody.scrollTop = messageBody.scrollHeight; }

    // 从全局配置读取安全变量
    var sellerId = window.chatConfig.sellerId;
    var chatRoomId = window.chatConfig.chatRoomId;

    // 1. 动态获取匹配商家的产品列表
    var sellerProductsModal = document.getElementById('sellerProductsModal');
    if (sellerProductsModal) {
        sellerProductsModal.addEventListener('show.bs.modal', function () {
            var container = document.getElementById("sellerProductsContainer");
            fetch(`/Chat/GetSellerProducts?sellerId=${sellerId}`)
                .then(response => response.json())
                .then(data => {
                    if(data.length === 0){
                        container.innerHTML = '<div class="text-center py-3 text-muted">该商家暂未上架其他商品</div>';
                        return;
                    }
                    let html = '<div class="list-group list-group-flush" style="max-height: 400px; overflow-y: auto;">';
                    
                    data.forEach(p => {
                        html += `
                            <div class="list-group-item d-flex align-items-center justify-content-between p-2 cursor-pointer border-bottom" onclick="sendCustomCard('PRODUCT','${encodeURIComponent(p.name)}','${p.productId}','${p.price}','${p.imagePath}')">
                                <div class="d-flex align-items-center text-truncate me-2">
                                    <img src="${p.imagePath}" class="rounded border me-2" style="width:40px;height:40px;object-fit:cover;"/>
                                    <div class="text-truncate" style="font-size:13px;">
                                        <b class="d-block text-dark text-truncate" style="max-width:240px;">${p.name}</b>
                                        <span class="text-danger">RM ${p.price}</span>
                                    </div>
                                </div>
                                <button class="btn btn-primary btn-sm rounded-pill px-3" style="font-size:11px;">发送</button>
                            </div>`;
                    });
                    html += '</div>';
                    container.innerHTML = html;
                }).catch(() => {
                    container.innerHTML = '<div class="text-center py-3 text-danger">产品加载失败</div>';
                });
        });
    }

    // 2. 动态获取匹配商家的历史订单列表
    var customerOrdersModal = document.getElementById('customerOrdersModal');
    if (customerOrdersModal) {
        customerOrdersModal.addEventListener('show.bs.modal', function () {
            var container = document.getElementById("customerOrdersContainer");
            fetch(`/Chat/GetCustomerOrders?sellerId=${sellerId}`)
                .then(response => response.json())
                .then(data => {
                    if(data.length === 0){
                        container.innerHTML = '<div class="text-center py-3 text-muted">暂无与该商家相关的订单记录</div>';
                        return;
                    }
                    let html = '<div class="list-group list-group-flush" style="max-height: 400px; overflow-y: auto;">';
                    
                    data.forEach(o => {
                        html += `
                        <div class="list-group-item d-flex align-items-center justify-content-between p-2 border-bottom">
                                <div class="d-flex align-items-center text-truncate me-2" style="flex-grow:1;">
                                    <img src="${o.coverImage || '/images/default-order.png'}" class="rounded border me-2" style="width:45px;height:45px;object-fit:cover;"/>
                                    <div class="text-truncate" style="font-size:12px; line-height:1.3;">
                                        <b class="text-dark d-block">订单号: #${o.orderId}</b>
                                        <span class="text-muted d-block text-truncate" style="max-width:200px;">${o.coverName}</span>
                                        <span class="text-danger fw-bold">RM ${o.totalAmount}</span>
                                    </div>
                                </div>
                                <button type="button" class="btn btn-info text-white btn-sm rounded-pill py-1 px-3" style="font-size:11px;" 
                                        onclick="sendCustomCard('ORDER', '', '${o.orderId}', '${o.totalAmount}', '${o.coverImage || ''}')">
                                    发送
                                </button>
                            </div>`;
                    });
                    html += '</div>';
                    container.innerHTML = html;
                }).catch(() => {
                    container.innerHTML = '<div class="text-center py-3 text-danger">订单加载失败</div>';
                });
        });
    }

    // 3. 快捷商品栏发送
    var sendProductBtn = document.getElementById("sendProductLinkBtn");
    if (sendProductBtn && window.chatConfig.hasChatProduct) {
        sendProductBtn.addEventListener("click", function () {
            sendCustomCard(
                'PRODUCT', 
                window.chatConfig.chatProductName,
                window.chatConfig.chatProductId, 
                window.chatConfig.chatProductPrice,
                window.chatConfig.chatProductImagePath
            );
        });
    }

    // 4. 发照片逻辑
    var attachPhotoBtn = document.getElementById("attachPhotoBtn");
    var photoFileInput = document.getElementById("photoFileInput");
    if (attachPhotoBtn && photoFileInput) {
        attachPhotoBtn.addEventListener("click", function () {
            photoFileInput.click();
        });
        photoFileInput.addEventListener("change", function () {
            var file = this.files[0];
            if (!file) return;
            if (!file.type.startsWith("image/")) {
                alert("请选择有效的图片文件！");
                return;
            }
            if (file.size > 5 * 1024 * 1024) {
                alert("图片太大了，请上传 5MB 以内的图片");
                return;
            }
            var reader = new FileReader();
            reader.onload = function (e) {
                var base64Data = e.target.result;
                var input = document.getElementById("messageInput");
                var form = document.getElementById("chatForm");
                if (input && form) {
                    input.value = "[IMAGE_CARD]<div class='p-1 bg-white rounded shadow-sm border text-center' style='max-width: 200px;'>\n" +
                        "  <img src='" + base64Data + "' class='img-fluid rounded' style='max-height: 200px; object-fit: contain;' />\n" +
                        "</div>";
                    input.removeAttribute("required");
                    form.submit();
                }
            };
            reader.readAsDataURL(file);
        });
    }
});

// 5. 底层发卡片函数（保持全局作用域，确保 HTML onclick 能够正常调用）
function sendCustomCard(type, title, id, price, img) {
    var input = document.getElementById("messageInput");
    var form = document.getElementById("chatForm");
    
    if(type === 'PRODUCT') {
        input.value = "[PRODUCT_CARD]" +
            "<div class='product-card-bubble'>" +
            "<a href='/Customer/ProductDetails/" + id + "' " +
            "class='d-block text-decoration-none bg-white p-2 rounded shadow-sm border border-info-subtle' " +
            "style='max-width:260px; transition:0.2s;'>" +
            "<div class='p-1'>" +
            "<div class='text-muted border-bottom pb-1 mb-2 small d-flex justify-content-between' style='font-size:11px;'>\n" +
            "<span>Products</span><span class='text-primary fw-bold'>Click to view details</span>" +
            "</div>" +
            "<div class='d-flex align-items-center mb-2'>" +
            "<img src='" + img + "' class='rounded border me-2' style='width:55px;height:55px;object-fit:cover;' />" +
            "<div class='text-truncate' style='line-height:1.2;'>" +
            "<span class='d-block fw-bold text-dark text-truncate' style='font-size:12px;'>" + title + "</span>" +
            "<small class='text-muted d-block text-truncate' style='font-size:11px;'>Go to product page</small>" +
            "</div>" +
            "</div>" +
            "<div class='text-dark small d-flex justify-content-between align-items-center border-top pt-1 mt-1' style='font-size:12px;'>" +
            "<span>Price: <b class='text-danger'>RM " + price + "</b></span>" +
            "<span class='text-primary' style='font-size:11px;'>View <i class='bi bi-chevron-right'></i></span>" +
            "</div>" +
            "</div>" +
            "</a>" +
            "</div>";
    }
    else if(type === 'ORDER') {
        input.value = "[ORDER_TAG]|" + id + "|" + price + "|" + img;
    }
    
    input.removeAttribute("required");
    form.submit();
}

// 6. 图片大图预览
function previewChatImage(element) {
    var img = element.querySelector('img');
    if (img) {
        var src = img.getAttribute('src');
        document.getElementById('previewModalImage').src = src;
        var myModal = new bootstrap.Modal(document.getElementById('imagePreviewModal'));
        myModal.show();
    }
}