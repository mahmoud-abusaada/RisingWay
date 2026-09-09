using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.UI;

public class ProductItem : MonoBehaviour
{
    public string productId;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI price;
    [SerializeField] private Button purchaseButton;
    private Product product;

    public delegate void PurchaseEvent(Product model, Action onComplete);
    public event PurchaseEvent OnPurchase;

    public void Setup(Product product)
    {
        if (product == null)
            return;

        this.product = product;
        // Not working
        // title.text = "x " + product.definition.payout.quantity;
        // if (float.TryParse(product.metadata.localizedPriceString, out _))
        //     price.text = $"{product.metadata.localizedPriceString} {product.metadata.isoCurrencyCode}";
        // else
        //     price.text = product.metadata.localizedPriceString;
        string priceStr = String.Format("{0:0.00}", product.metadata.localizedPrice);
        price.text = $"{priceStr} {product.metadata.isoCurrencyCode}";
    }

    public void Purchase()
    {
        purchaseButton.enabled = false;
        OnPurchase?.Invoke(product, HandlePurchaseComplete);
    }

    public void HandlePurchaseComplete()
    {
        purchaseButton.enabled = true;
    }
}
