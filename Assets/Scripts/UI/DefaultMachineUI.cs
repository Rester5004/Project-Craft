using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
public class DefaultMachineUI : MonoBehaviour
{
    [Header("InputSlots")]
    [SerializeField] private GameObject inputSlot1;
    [SerializeField] private GameObject inputSlot2;
    [SerializeField] private GameObject inputSlot3;

    [Header("ProgressBar")]
    [SerializeField] private GameObject progressBar;

    [Header("OutputSlots")]
    [SerializeField] private GameObject outputSlot1;
    [SerializeField] private GameObject outputSlot2;
    [SerializeField] private GameObject outputSlot3;
    [SerializeField] private GameObject outputSlot4;
    [SerializeField] private GameObject outputSlot5;
    [SerializeField] private GameObject outputSlot6;

    [Header("GasBar")]
    [SerializeField] private GameObject gasBar1;
    [SerializeField] private GameObject gasBar2;

    [Header("EnergyBar")]
    [SerializeField] private GameObject energyBar;

    [Header("MachineName")]
    [SerializeField] private TMP_Text machineName;


}
