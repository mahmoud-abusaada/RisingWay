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
        // Remove Ads, once owned: say so, and do not offer it again. Checked before the product,
        // so it shows even while the store is unreachable - ownership is in the save file.
        bool owned = productId == IapStore.REMOVE_ADS && !PlayerStats.Instance.isAdEnabled();
        purchaseButton.interactable = !owned;
        Transform buyNow = purchaseButton.transform.Find("BuyNow");
        if (buyNow != null)
            buyNow.gameObject.SetActive(!owned);
        if (owned)
        {
            price.text = "OWNED";
            return;
        }

        if (product == null)
            return;

        this.product = product;
        // if (float.TryParse(product.metadata.localizedPriceString, out _))
        //     price.text = $"{product.metadata.localizedPriceString} {product.metadata.isoCurrencyCode}";
        // else
        //     price.text = product.metadata.localizedPriceString;
        string priceStr = String.Format("{0:0.00}", product.metadata.localizedPrice);
        price.text = $"{priceStr} {product.metadata.isoCurrencyCode}";

        // The amount comes from the same table the purchase pays out (IapStore.PackAmounts), so
        // the shop cannot advertise a number the game does not grant. The scene's own label is
        // left alone for anything without an amount, like Remove Ads. It used to read the payout
        // from the catalog, which Unity IAP does not fill in - the "Not working" note here.
        int amount;
        if (title != null && IapStore.PackAmounts.TryGetValue(productId, out amount))
            title.text = "x " + Utility.getFormatedNumber(amount);
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
