using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DonationController : MonoBehaviour
{
    private void OnEnable()
    {
        APIConnecter.OnDonation += DonationHandler;
    }

    private void OnDisable()
    {
        APIConnecter.OnDonation -= DonationHandler;
    }

    private void DonationHandler(string nickname, int count)
    {
        Debug.Log($"[별풍선] {nickname} / {count}개");
    }
}